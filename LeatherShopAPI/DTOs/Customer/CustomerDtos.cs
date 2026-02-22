using System.ComponentModel.DataAnnotations;

namespace LeatherShopAPI.DTOs.Customer;

public class CreateCustomerDto
{
    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[1-9]\d{6,14}$", ErrorMessage = "Invalid phone number format.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string? Name { get; set; }

    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    public string? Address { get; set; }
}

public class BulkImportCustomerItem
{
    [Required(ErrorMessage = "Phone number is required.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }
}

public class BulkImportDto
{
    [Required(ErrorMessage = "Customer list is required.")]
    [MinLength(1, ErrorMessage = "At least one customer is required.")]
    public List<BulkImportCustomerItem> Customers { get; set; } = new();
}

public class CustomerListDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsSubscribed { get; set; }
    public DateTime CreatedAt { get; set; }
    public int OrderCount { get; set; }
}

public class CustomerCountDto
{
    public int SubscriberCount { get; set; }
    public int TotalCount { get; set; }
}

public class CustomerCreatedDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class BulkImportResultDto
{
    public string Message { get; set; } = string.Empty;
    public int Imported { get; set; }
    public int SkippedDuplicates { get; set; }
}
