using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services;

/// <summary>
/// Generates a professional PDF invoice for an order.
/// Uses QuestPDF (Community License) for layout and rendering.
/// </summary>
public class InvoicePdfService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<InvoicePdfService> _logger;

    public InvoicePdfService(IWebHostEnvironment env, ILogger<InvoicePdfService> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Generates a PDF invoice for the given order.
    /// Order must have Customer, OrderItems with Product and Product.Images eagerly loaded.
    /// </summary>
    public byte[] GenerateInvoice(Order order)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, order));
                page.Content().Element(c => ComposeContent(c, order));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, Order order)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Leather Shop").FontSize(22).Bold().FontColor("#1e293b");
                    left.Item().Text("Premium Handcrafted Leather Products").FontSize(9).FontColor("#64748b");
                });

                row.ConstantItem(180).AlignRight().Column(right =>
                {
                    right.Item().Text("INVOICE").FontSize(18).Bold().FontColor("#3b82f6");
                    right.Item().Text($"#{order.OrderNumber}").FontSize(10).FontColor("#64748b");
                });
            });

            col.Item().PaddingVertical(10).LineHorizontal(1).LineColor("#e2e8f0");

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("Bill To:").SemiBold().FontColor("#475569");
                    left.Item().Text(string.IsNullOrEmpty(order.Customer?.Name) ? "Customer" : order.Customer.Name);
                    left.Item().Text(order.Customer?.PhoneNumber ?? "");
                    if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
                        left.Item().Text(order.ShippingAddress).FontSize(9).FontColor("#64748b");
                });

                row.ConstantItem(180).AlignRight().Column(right =>
                {
                    right.Item().Text($"Date: {order.CreatedAt:dd MMM yyyy}");
                    right.Item().Text($"Status: {order.Status}");
                    right.Item().Text($"Payment: {(order.IsPaid ? "Paid" : "Unpaid")}").FontColor(order.IsPaid ? "#16a34a" : "#dc2626");
                });
            });

            col.Item().PaddingTop(15);
        });
    }

    private void ComposeContent(IContainer container, Order order)
    {
        container.Column(col =>
        {
            // Table header
            col.Item().Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(0).Row(headerRow =>
            {
                headerRow.ConstantItem(60).Padding(8).Text("Image").SemiBold().FontSize(9).FontColor("#475569");
                headerRow.RelativeItem().Padding(8).Text("Product").SemiBold().FontSize(9).FontColor("#475569");
                headerRow.ConstantItem(50).Padding(8).AlignCenter().Text("Qty").SemiBold().FontSize(9).FontColor("#475569");
                headerRow.ConstantItem(80).Padding(8).AlignRight().Text("Price").SemiBold().FontSize(9).FontColor("#475569");
                headerRow.ConstantItem(90).Padding(8).AlignRight().Text("Subtotal").SemiBold().FontSize(9).FontColor("#475569");
            });

            // Table rows
            foreach (var item in order.OrderItems)
            {
                var imageBytes = TryLoadImage(item);

                col.Item().BorderBottom(1).BorderColor("#f0f0f0").Padding(0).Row(dataRow =>
                {
                    // Image cell
                    dataRow.ConstantItem(60).Padding(6).AlignCenter().AlignMiddle().Element(imgContainer =>
                    {
                        if (imageBytes != null)
                        {
                            imgContainer.Width(44).Height(44).Image(imageBytes);
                        }
                        else
                        {
                            imgContainer.Width(44).Height(44)
                                .Background("#f1f5f9").AlignCenter().AlignMiddle()
                                .Text("📷").FontSize(16);
                        }
                    });

                    // Product name
                    dataRow.RelativeItem().Padding(8).AlignMiddle()
                        .Text(item.Product?.Name ?? "Unknown Product").FontSize(10);

                    // Quantity
                    dataRow.ConstantItem(50).Padding(8).AlignCenter().AlignMiddle()
                        .Text(item.Quantity.ToString());

                    // Unit price
                    dataRow.ConstantItem(80).Padding(8).AlignRight().AlignMiddle()
                        .Text($"₹{item.UnitPrice:N0}");

                    // Subtotal
                    dataRow.ConstantItem(90).Padding(8).AlignRight().AlignMiddle()
                        .Text($"₹{item.UnitPrice * item.Quantity:N0}").SemiBold();
                });
            }

            // Total row
            col.Item().PaddingTop(10).Row(totalRow =>
            {
                totalRow.RelativeItem(); // spacer
                totalRow.ConstantItem(200).Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(12).Row(inner =>
                {
                    inner.RelativeItem().Text("Total Amount").FontSize(13).Bold();
                    inner.ConstantItem(100).AlignRight().Text($"₹{order.TotalAmount:N0}").FontSize(13).Bold().FontColor("#1e293b");
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Thank you for your purchase! ").FontSize(9).FontColor("#64748b");
            text.Span("• Leather Shop").FontSize(9).FontColor("#94a3b8");
        });
    }

    /// <summary>
    /// Tries to load the image for an order item:
    ///   1. If SelectedImageId is set, look up the ProductImage → use its URL
    ///   2. Otherwise fall back to Product.ImageUrl (primary)
    ///   3. Read from wwwroot (local paths) — external URLs are skipped for PDF (they'd slow it down)
    /// Returns null if image can't be loaded (graceful degradation).
    /// </summary>
    private byte[]? TryLoadImage(OrderItem item)
    {
        try
        {
            string? imageUrl = null;

            // Resolve selected image
            if (item.SelectedImageId.HasValue && item.Product?.Images != null)
            {
                var selectedImg = item.Product.Images.FirstOrDefault(pi => pi.Id == item.SelectedImageId.Value);
                if (selectedImg != null)
                    imageUrl = selectedImg.ImageUrl;
            }

            // Fallback to primary
            if (string.IsNullOrEmpty(imageUrl))
                imageUrl = item.Product?.ImageUrl;

            if (string.IsNullOrEmpty(imageUrl))
                return null;

            // Only load local files (paths starting with / like /uploads/products/...)
            // Skip external URLs to avoid HTTP calls during PDF generation
            if (imageUrl.StartsWith("http"))
            {
                _logger.LogDebug("Skipping external image URL for PDF: {Url}", imageUrl);
                return null;
            }

            var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Image file not found for PDF: {Path}", filePath);
                return null;
            }

            return File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image for OrderItem {OrderItemId}", item.Id);
            return null;
        }
    }
}
