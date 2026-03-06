using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Customer;
using LeatherShopAPI.Models;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Services.Interfaces;
using static LeatherShopAPI.Extensions.SqlHelper;

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

    public async Task<PaginatedResult<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (subscribedOnly == true)
            query = query.Where(c => c.IsSubscribed);

        if (!string.IsNullOrEmpty(search))
        {
            var escaped = EscapeLikePattern(search);
            query = query.Where(c => EF.Functions.ILike(c.PhoneNumber, $"%{escaped}%") || EF.Functions.ILike(c.Name, $"%{escaped}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListDto
            {
                Id = c.Id,
                PhoneNumber = c.PhoneNumber,
                Name = c.Name,
                Address = c.Address,
                IsSubscribed = c.IsSubscribed,
                CreatedAt = c.CreatedAt,
                OrderCount = c.Orders.Count
            }).ToListAsync(ct);

        return new PaginatedResult<CustomerListDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerCountDto> GetCountAsync(CancellationToken ct = default)
    {
        return new CustomerCountDto
        {
            SubscriberCount = await _db.Customers.CountAsync(c => c.IsSubscribed, ct),
            TotalCount = await _db.Customers.CountAsync(ct)
        };
    }

    public async Task<CustomerCreatedDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        var phone = PhoneNumberHelper.Normalize(dto.PhoneNumber);
        if (string.IsNullOrEmpty(phone) || phone.Length < 5)
            throw new ArgumentException("Invalid phone number.");

        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
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
        await _db.SaveChangesAsync(ct);

        // Send welcome message via WhatsApp
        try
        {
            var welcomeMsg = $"👋 Welcome to *Cuir Galerie*{(string.IsNullOrEmpty(customer.Name) ? "" : $", {customer.Name}")}!\n\n" +
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

    public async Task<BulkImportResultDto> BulkImportAsync(BulkImportDto dto, CancellationToken ct = default)
    {
        if (dto.Customers == null || !dto.Customers.Any())
            throw new ArgumentException("No customers provided");

        if (dto.Customers.Count > 1000)
            throw new ArgumentException("Maximum 1000 customers per import. Please split into smaller batches.");

        // Only check imported phone numbers against DB (not all customers)
        var importedPhones = dto.Customers
            .Select(c => PhoneNumberHelper.Normalize(c.PhoneNumber))
            .Where(p => !string.IsNullOrEmpty(p) && p.Length >= 5)
            .ToList();

        var existingPhones = (await _db.Customers
            .Where(c => importedPhones.Contains(c.PhoneNumber))
            .Select(c => c.PhoneNumber)
            .ToListAsync(ct))
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

        await _db.SaveChangesAsync(ct);

        return new BulkImportResultDto
        {
            Message = $"Import complete. Added: {added}, Skipped (duplicates): {skipped}",
            Imported = added,
            SkippedDuplicates = skipped
        };
    }

    public async Task<CustomerListDto?> UpdateAsync(int id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer == null) return null;

        if (dto.Name != null) customer.Name = dto.Name.Trim();
        if (dto.Address != null) customer.Address = dto.Address.Trim();
        if (dto.IsSubscribed.HasValue) customer.IsSubscribed = dto.IsSubscribed.Value;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // No WhatsApp message is sent on edit - this is intentional
        return new CustomerListDto
        {
            Id = customer.Id,
            PhoneNumber = customer.PhoneNumber,
            Name = customer.Name,
            Address = customer.Address,
            IsSubscribed = customer.IsSubscribed,
            CreatedAt = customer.CreatedAt,
            OrderCount = await _db.Orders.CountAsync(o => o.CustomerId == id, ct)
        };
    }

    public async Task<DeleteCustomerResponse> DeleteAsync(int id, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer == null)
            return new DeleteCustomerResponse { Result = DeleteCustomerResult.NotFound };

        // Prevent deletion when orders exist - order history is needed for accounting/compliance
        var orderCount = await _db.Orders.CountAsync(o => o.CustomerId == id, ct);
        if (orderCount > 0)
            return new DeleteCustomerResponse
            {
                Result = DeleteCustomerResult.HasOrders,
                OrderCount = orderCount
            };

        // CartItems and ChatMessages still cascade-delete (transient data)
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(ct);
        return new DeleteCustomerResponse { Result = DeleteCustomerResult.Deleted };
    }

    public async Task<bool> ToggleSubscriptionAsync(int id, bool isSubscribed, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FindAsync(new object[] { id }, ct);
        if (customer == null) return false;

        customer.IsSubscribed = isSubscribed;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
