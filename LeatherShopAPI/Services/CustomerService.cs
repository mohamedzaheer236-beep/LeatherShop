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

    public async Task<PaginatedResult<CustomerListDto>> GetAllAsync(bool? subscribedOnly, string? search, string? category, int page = 1, int pageSize = 25,
        string? sortField = null, string? sortOrder = null, string? name = null, string? phone = null, string? address = null,
        string? dateFrom = null, string? dateTo = null, int? orderCountMin = null, int? orderCountMax = null, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (subscribedOnly == true)
            query = query.Where(c => c.IsSubscribed);

        if (!string.IsNullOrEmpty(category) && Enum.TryParse<CustomerCategory>(category, ignoreCase: true, out var parsedCategory))
            query = query.Where(c => c.Category == parsedCategory);

        if (!string.IsNullOrEmpty(search))
        {
            var escaped = EscapeLikePattern(search);
            query = query.Where(c => EF.Functions.ILike(c.PhoneNumber, $"%{escaped}%") || EF.Functions.ILike(c.Name, $"%{escaped}%"));
        }

        // Column filters
        if (!string.IsNullOrWhiteSpace(name))
        {
            var escaped = EscapeLikePattern(name.Trim());
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{escaped}%"));
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var escaped = EscapeLikePattern(phone.Trim());
            query = query.Where(c => EF.Functions.ILike(c.PhoneNumber, $"%{escaped}%"));
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            var escaped = EscapeLikePattern(address.Trim());
            query = query.Where(c => EF.Functions.ILike(c.Address, $"%{escaped}%"));
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var fromDate))
        {
            var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(c => c.CreatedAt >= start);
        }

        if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var toDate))
        {
            var end = toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
            query = query.Where(c => c.CreatedAt < end);
        }

        // Order count filters — applied after projection below
        // For now, filter in the projected queryable

        // Project first so we can sort/filter on OrderCount
        var projected = query.Select(c => new CustomerListDto
        {
            Id = c.Id,
            PhoneNumber = c.PhoneNumber,
            Name = c.Name,
            Address = c.Address,
            IsSubscribed = c.IsSubscribed,
            Category = c.Category.ToString(),
            CreatedAt = c.CreatedAt,
            OrderCount = c.Orders.Count
        });

        if (orderCountMin.HasValue)
            projected = projected.Where(c => c.OrderCount >= orderCountMin.Value);

        if (orderCountMax.HasValue)
            projected = projected.Where(c => c.OrderCount <= orderCountMax.Value);

        var totalCount = await projected.CountAsync(ct);

        // Sorting
        var isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        projected = (sortField?.ToLower()) switch
        {
            "name" => isDesc ? projected.OrderByDescending(c => c.Name) : projected.OrderBy(c => c.Name),
            "phonenumber" => isDesc ? projected.OrderByDescending(c => c.PhoneNumber) : projected.OrderBy(c => c.PhoneNumber),
            "address" => isDesc ? projected.OrderByDescending(c => c.Address) : projected.OrderBy(c => c.Address),
            "category" => isDesc ? projected.OrderByDescending(c => c.Category) : projected.OrderBy(c => c.Category),
            "issubscribed" => isDesc ? projected.OrderByDescending(c => c.IsSubscribed) : projected.OrderBy(c => c.IsSubscribed),
            "ordercount" => isDesc ? projected.OrderByDescending(c => c.OrderCount) : projected.OrderBy(c => c.OrderCount),
            _ => isDesc ? projected.OrderByDescending(c => c.CreatedAt) : projected.OrderBy(c => c.CreatedAt),
        };

        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

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
        if (string.IsNullOrEmpty(phone) || phone.Length < 7 || phone.Length > 15 || !phone.All(char.IsDigit))
            throw new ArgumentException("Invalid phone number.");

        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
        if (existing != null)
            throw new InvalidOperationException($"Customer with phone {phone} already exists.");

        var customer = new Customer
        {
            PhoneNumber = phone,
            Name = dto.Name?.Trim() ?? "",
            Address = dto.Address?.Trim() ?? "",
            IsSubscribed = true,
            Category = Enum.TryParse<CustomerCategory>(dto.Category, ignoreCase: true, out var cat)
                ? cat : throw new ArgumentException($"Invalid category: {dto.Category}. Valid values: Reseller, DirectCorporate, FriendsAndFamily, UtilityOnly")
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        // Send welcome message via WhatsApp using store_notification (UTILITY — no frequency caps).
        var welcomeSent = false;
        try
        {
            var nameParam = string.IsNullOrEmpty(customer.Name) ? "there" : customer.Name;
            var welcomeText = $"Hello {nameParam}, your account at Cuir Galerie has been created successfully. " +
                "You can reach us anytime on WhatsApp for order updates and support. Reply Hi to get started.";
            await _whatsApp.SendTemplateMessage(
                to: phone,
                templateName: "store_notification",
                languageCode: "en",
                parameters: new List<string> { welcomeText });
            welcomeSent = true;
            _logger.LogInformation("Welcome template sent to {Phone}", phone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send welcome template to {Phone} — customer was still added", phone);
        }

        return new CustomerCreatedDto
        {
            Id = customer.Id,
            PhoneNumber = customer.PhoneNumber,
            Name = customer.Name,
            WelcomeSent = welcomeSent
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
            if (string.IsNullOrEmpty(phone) || phone.Length < 7 || phone.Length > 15 || !phone.All(char.IsDigit)) { skipped++; continue; }

            if (existingPhones.Contains(phone)) { skipped++; continue; }

            existingPhones.Add(phone); // prevent duplicates within the same import batch
            var importCategory = CustomerCategory.FriendsAndFamily;
            if (!string.IsNullOrEmpty(item.Category) && Enum.TryParse<CustomerCategory>(item.Category, ignoreCase: true, out var parsedCat))
                importCategory = parsedCat;

            _db.Customers.Add(new Customer
            {
                PhoneNumber = phone,
                Name = item.Name?.Trim() ?? "",
                Address = item.Address?.Trim() ?? "",
                IsSubscribed = true,
                Category = importCategory
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
        if (dto.Category != null && Enum.TryParse<CustomerCategory>(dto.Category, ignoreCase: true, out var updatedCat))
            customer.Category = updatedCat;
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
            Category = customer.Category.ToString(),
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

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
    {
        var normalized = PhoneNumberHelper.Normalize(phone);
        return await _db.Customers.AnyAsync(c => c.PhoneNumber == normalized, ct);
    }

    public async Task<List<string>> CheckPhonesAsync(List<string> phones, CancellationToken ct = default)
    {
        var normalized = phones
            .Select(p => PhoneNumberHelper.Normalize(p))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        return await _db.Customers
            .Where(c => normalized.Contains(c.PhoneNumber))
            .Select(c => c.PhoneNumber)
            .ToListAsync(ct);
    }

    public async Task<BulkDeleteResultDto> BulkDeleteAsync(List<int> ids, CancellationToken ct = default)
    {
        var customers = await _db.Customers
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, OrderCount = c.Orders.Count })
            .ToListAsync(ct);

        var toDelete = customers.Where(c => c.OrderCount == 0).Select(c => c.Id).ToList();
        var skipped = customers.Count(c => c.OrderCount > 0);

        if (toDelete.Count > 0)
        {
            await _db.Customers
                .Where(c => toDelete.Contains(c.Id))
                .ExecuteDeleteAsync(ct);
        }

        return new BulkDeleteResultDto
        {
            Deleted = toDelete.Count,
            SkippedWithOrders = skipped,
            Message = skipped > 0
                ? $"Deleted {toDelete.Count} customer(s). {skipped} customer(s) with orders were skipped."
                : $"Deleted {toDelete.Count} customer(s) successfully."
        };
    }
}
