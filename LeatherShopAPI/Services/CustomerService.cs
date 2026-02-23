using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(AppDbContext db, IWhatsAppService whatsApp, ILogger<CustomerService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
    }

    public async Task<List<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search)
    {
        var query = _db.Customers.AsQueryable();

        if (subscribedOnly == true)
            query = query.Where(c => c.IsSubscribed);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.PhoneNumber.Contains(search) || c.Name.ToLower().Contains(search.ToLower()));

        return await query.OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerListDto
            {
                Id = c.Id,
                PhoneNumber = c.PhoneNumber,
                Name = c.Name,
                Address = c.Address,
                IsSubscribed = c.IsSubscribed,
                CreatedAt = c.CreatedAt,
                OrderCount = c.Orders.Count
            }).ToListAsync();
    }

    public async Task<CustomerCountDto> GetCountAsync()
    {
        return new CustomerCountDto
        {
            SubscriberCount = await _db.Customers.CountAsync(c => c.IsSubscribed),
            TotalCount = await _db.Customers.CountAsync()
        };
    }

    public async Task<CustomerCreatedDto> CreateAsync(CreateCustomerDto dto)
    {
        var phone = PhoneNumberHelper.Normalize(dto.PhoneNumber);
        if (string.IsNullOrEmpty(phone) || phone.Length < 5)
            throw new ArgumentException("Invalid phone number.");

        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
        if (existing != null)
            throw new InvalidOperationException($"Customer with phone {phone} already exists.");

        var customer = new Customer
        {
            PhoneNumber = phone,
            Name = dto.Name?.Trim() ?? "",
            Address = dto.Address?.Trim() ?? "",
            IsSubscribed = true
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        // Send welcome message via WhatsApp
        try
        {
            var welcomeMsg = $"👋 Welcome to *Leather Shop*{(string.IsNullOrEmpty(customer.Name) ? "" : $", {customer.Name}")}!\n\n" +
                "We're glad to have you. You can browse our products and place orders right here on WhatsApp.\n\n" +
                "Type *Hi* to get started!";
            await _whatsApp.SendTextMessage(phone, welcomeMsg);
            _logger.LogInformation("Welcome message sent to {Phone}", phone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send welcome message to {Phone} - customer was still added", phone);
        }

        return new CustomerCreatedDto
        {
            Id = customer.Id,
            PhoneNumber = customer.PhoneNumber,
            Name = customer.Name
        };
    }

    public async Task<BulkImportResultDto> BulkImportAsync(BulkImportDto dto)
    {
        if (dto.Customers == null || !dto.Customers.Any())
            throw new ArgumentException("No customers provided");

        // Load all existing phone numbers into a HashSet to avoid N+1 queries
        var existingPhones = (await _db.Customers.Select(c => c.PhoneNumber).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0, skipped = 0;

        foreach (var item in dto.Customers)
        {
            var phone = PhoneNumberHelper.Normalize(item.PhoneNumber);
            if (string.IsNullOrEmpty(phone) || phone.Length < 5) { skipped++; continue; }

            if (existingPhones.Contains(phone)) { skipped++; continue; }

            existingPhones.Add(phone); // prevent duplicates within the same import batch
            _db.Customers.Add(new Customer
            {
                PhoneNumber = phone,
                Name = item.Name?.Trim() ?? "",
                Address = item.Address?.Trim() ?? "",
                IsSubscribed = true
            });
            added++;
        }

        await _db.SaveChangesAsync();

        return new BulkImportResultDto
        {
            Message = $"Import complete. Added: {added}, Skipped (duplicates): {skipped}",
            Imported = added,
            SkippedDuplicates = skipped
        };
    }

    public async Task<bool> ToggleSubscriptionAsync(int id, bool isSubscribed)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return false;

        customer.IsSubscribed = isSubscribed;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
