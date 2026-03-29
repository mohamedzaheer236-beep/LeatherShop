using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Generates a professionally designed PDF invoice for an order.
/// Uses QuestPDF (Community License) with a premium dark/gold color scheme.
/// </summary>
public class InvoicePdfService : IInvoicePdfService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<InvoicePdfService> _logger;

    // Brand color palette
    private const string BrandBlack = "#1a1a2e";
    private const string BrandGold = "#C9A96E";
    private const string BrandGoldLight = "#E8D5B5";
    private const string TextDark = "#1e293b";
    private const string TextMuted = "#64748b";
    private const string TextLight = "#94a3b8";
    private const string BorderLight = "#e2e8f0";
    private const string BgSubtle = "#f8fafc";
    private const string StatusGreen = "#16a34a";
    private const string StatusRed = "#dc2626";
    private const string StatusBlue = "#3b82f6";
    private const string StatusAmber = "#d97706";
    private const string White = "#ffffff";

    public InvoicePdfService(IWebHostEnvironment env, ILogger<InvoicePdfService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public byte[] GenerateInvoice(Order order)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextDark));

                page.Header().Element(c => ComposeHeader(c, order));
                page.Content().Element(c => ComposeContent(c, order));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    // ───────── HEADER ─────────

    private void ComposeHeader(IContainer container, Order order)
    {
        container.Column(col =>
        {
            // Dark top banner
            col.Item().Background(BrandBlack).Padding(30).PaddingBottom(24).Row(row =>
            {
                // Left: Logo/Brand
                row.RelativeItem().Column(left =>
                {
                    // Try to load company logo
                    var logoBytes = TryLoadLogo();
                    if (logoBytes != null)
                    {
                        left.Item().Width(60).Height(60).Image(logoBytes);
                        left.Item().PaddingTop(8).Text("Cuir Galerie").FontSize(20).Bold().FontColor(White);
                    }
                    else
                    {
                        // Elegant text-only branding
                        left.Item().Text("CG").FontSize(32).Bold().FontColor(BrandGold).LetterSpacing(0.05f);
                        left.Item().PaddingTop(4).Text("Cuir Galerie").FontSize(18).Bold().FontColor(White).LetterSpacing(0.02f);
                    }
                    left.Item().PaddingTop(2).Text("Premium Handcrafted Leather Products").FontSize(8).FontColor(BrandGoldLight).LetterSpacing(0.05f);
                });

                // Right: Invoice title + number
                row.ConstantItem(200).AlignRight().AlignBottom().Column(right =>
                {
                    right.Item().AlignRight().Text("INVOICE").FontSize(28).Bold().FontColor(BrandGold).LetterSpacing(0.1f);
                    right.Item().PaddingTop(4).AlignRight().Text($"#{order.OrderNumber}").FontSize(11).FontColor(TextLight);
                });
            });

            // Gold accent line
            col.Item().Height(3).Background(BrandGold);

            // Info section
            col.Item().PaddingHorizontal(30).PaddingTop(20).PaddingBottom(16).Row(row =>
            {
                // Left: Bill To
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("BILL TO").FontSize(8).Bold().FontColor(BrandGold).LetterSpacing(0.1f);
                    left.Item().PaddingTop(6).Text(string.IsNullOrEmpty(order.Customer?.Name) ? "Customer" : order.Customer.Name)
                        .FontSize(13).SemiBold().FontColor(TextDark);
                    left.Item().PaddingTop(2).Text(order.Customer?.PhoneNumber ?? "").FontSize(10).FontColor(TextMuted);
                    if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
                        left.Item().PaddingTop(2).Text(order.ShippingAddress).FontSize(9).FontColor(TextMuted).LineHeight(1.4f);
                });

                // Right: Invoice details grid
                row.ConstantItem(200).Column(right =>
                {
                    right.Item().Text("INVOICE DETAILS").FontSize(8).Bold().FontColor(BrandGold).LetterSpacing(0.1f);
                    right.Item().PaddingTop(8).Element(c => ComposeDetailRow(c, "Date", order.CreatedAt.ToString("dd MMM yyyy")));
                    right.Item().PaddingTop(4).Element(c => ComposeDetailRow(c, "Order ID", order.OrderNumber));
                    right.Item().PaddingTop(4).Element(c => ComposeStatusRow(c, "Status", order.Status.ToString(), GetStatusColor(order.Status)));
                    right.Item().PaddingTop(4).Element(c => ComposeStatusRow(c, "Payment", order.IsPaid ? "Paid" : "Unpaid", order.IsPaid ? StatusGreen : StatusRed));
                });
            });

            // Divider
            col.Item().PaddingHorizontal(30).LineHorizontal(1).LineColor(BorderLight);
        });
    }

    private static void ComposeDetailRow(IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(70).Text(label).FontSize(9).FontColor(TextMuted);
            row.RelativeItem().AlignRight().Text(value).FontSize(9).SemiBold().FontColor(TextDark);
        });
    }

    private static void ComposeStatusRow(IContainer container, string label, string value, string color)
    {
        container.Row(row =>
        {
            row.ConstantItem(70).Text(label).FontSize(9).FontColor(TextMuted);
            row.RelativeItem().AlignRight().Text(value).FontSize(9).Bold().FontColor(color);
        });
    }

    // ───────── CONTENT ─────────

    private void ComposeContent(IContainer container, Order order)
    {
        container.PaddingHorizontal(30).PaddingTop(16).Column(col =>
        {
            // Section title
            col.Item().PaddingBottom(10).Text("ORDER ITEMS").FontSize(8).Bold().FontColor(BrandGold).LetterSpacing(0.1f);

            // Table header
            col.Item().Background(BrandBlack).Padding(0).Row(headerRow =>
            {
                headerRow.ConstantItem(42).Padding(10).Text("#").SemiBold().FontSize(8).FontColor(BrandGoldLight);
                headerRow.ConstantItem(50).Padding(10).Text("Image").SemiBold().FontSize(8).FontColor(BrandGoldLight);
                headerRow.RelativeItem().Padding(10).Text("Product").SemiBold().FontSize(8).FontColor(BrandGoldLight);
                headerRow.ConstantItem(45).Padding(10).AlignCenter().Text("Qty").SemiBold().FontSize(8).FontColor(BrandGoldLight);
                headerRow.ConstantItem(80).Padding(10).AlignRight().Text("Price").SemiBold().FontSize(8).FontColor(BrandGoldLight);
                headerRow.ConstantItem(90).Padding(10).AlignRight().Text("Subtotal").SemiBold().FontSize(8).FontColor(BrandGoldLight);
            });

            // Table rows
            var index = 0;
            foreach (var item in order.OrderItems)
            {
                index++;
                var isEven = index % 2 == 0;
                var imageBytes = TryLoadImage(item);
                var subtotal = item.UnitPrice * item.Quantity;

                col.Item()
                    .Background(isEven ? BgSubtle : White)
                    .BorderBottom(1).BorderColor(BorderLight)
                    .Padding(0).Row(dataRow =>
                    {
                        // Row number
                        dataRow.ConstantItem(42).Padding(10).AlignMiddle()
                            .Text(index.ToString()).FontSize(9).FontColor(TextMuted);

                        // Image
                        dataRow.ConstantItem(50).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().AlignMiddle()
                            .Element(imgContainer =>
                            {
                                if (imageBytes != null)
                                {
                                    imgContainer.Width(40).Height(40).Image(imageBytes);
                                }
                                else
                                {
                                    imgContainer.Width(40).Height(40)
                                        .Background("#f1f5f9").AlignCenter().AlignMiddle()
                                        .Text("—").FontSize(14).FontColor(TextLight);
                                }
                            });

                        // Product name
                        dataRow.RelativeItem().Padding(10).AlignMiddle()
                            .Text(item.Product?.Name ?? "Unknown Product").FontSize(10).FontColor(TextDark);

                        // Quantity
                        dataRow.ConstantItem(45).Padding(10).AlignCenter().AlignMiddle()
                            .Text(item.Quantity.ToString()).FontSize(10);

                        // Unit price
                        dataRow.ConstantItem(80).Padding(10).AlignRight().AlignMiddle()
                            .Text($"₹{item.UnitPrice:N2}").FontSize(10).FontColor(TextMuted);

                        // Subtotal
                        dataRow.ConstantItem(90).Padding(10).AlignRight().AlignMiddle()
                            .Text($"₹{subtotal:N2}").FontSize(10).SemiBold().FontColor(TextDark);
                    });
            }

            // Totals section
            col.Item().PaddingTop(16).Row(totalRow =>
            {
                totalRow.RelativeItem(); // spacer

                totalRow.ConstantItem(230).Column(totalsCol =>
                {
                    // Subtotal line
                    totalsCol.Item().PaddingVertical(6).PaddingHorizontal(12).Row(r =>
                    {
                        r.RelativeItem().Text("Subtotal").FontSize(10).FontColor(TextMuted);
                        r.ConstantItem(100).AlignRight().Text($"₹{order.TotalAmount:N2}").FontSize(10).FontColor(TextDark);
                    });

                    // Divider
                    totalsCol.Item().PaddingHorizontal(12).LineHorizontal(1).LineColor(BorderLight);

                    // Grand total with gold accent
                    totalsCol.Item().Background(BrandBlack).Padding(12).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL").FontSize(13).Bold().FontColor(BrandGold);
                        r.ConstantItem(110).AlignRight().Text($"₹{order.TotalAmount:N2}").FontSize(14).Bold().FontColor(White);
                    });
                });
            });

            // Payment info note
            if (!string.IsNullOrEmpty(order.PaymentId) && order.IsPaid)
            {
                col.Item().PaddingTop(16).PaddingHorizontal(4).Row(r =>
                {
                    r.RelativeItem(); // spacer
                    r.ConstantItem(230).Background("#f0fdf4").Border(1).BorderColor("#bbf7d0").Padding(10).Column(c =>
                    {
                        c.Item().Text("✓ Payment Confirmed").FontSize(9).Bold().FontColor(StatusGreen);
                        c.Item().PaddingTop(2).Text($"Transaction ID: {order.PaymentId}").FontSize(8).FontColor(TextMuted);
                    });
                });
            }
        });
    }

    // ───────── FOOTER ─────────

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            // Gold accent line
            col.Item().PaddingHorizontal(30).LineHorizontal(2).LineColor(BrandGold);

            col.Item().PaddingHorizontal(30).PaddingVertical(14).Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(center =>
                {
                    center.Item().AlignCenter().Text("Thank you for your purchase!").FontSize(10).SemiBold().FontColor(TextDark);
                    center.Item().PaddingTop(4).AlignCenter().Text(text =>
                    {
                        text.Span("Cuir Galerie").FontSize(8).Bold().FontColor(BrandGold);
                        text.Span("  •  Premium Handcrafted Leather Products").FontSize(8).FontColor(TextLight);
                    });
                });
            });
        });
    }

    // ───────── HELPERS ─────────

    private static string GetStatusColor(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => StatusAmber,
            OrderStatus.Confirmed => StatusBlue,
            OrderStatus.Shipped => StatusBlue,
            OrderStatus.Delivered => StatusGreen,
            OrderStatus.Cancelled => StatusRed,
            _ => TextMuted
        };
    }

    /// <summary>Tries to load the company logo from wwwroot/images/logo.png.</summary>
    private byte[]? TryLoadLogo()
    {
        try
        {
            var logoPath = Path.Combine(_env.WebRootPath, "images", "logo.png");
            if (File.Exists(logoPath))
                return File.ReadAllBytes(logoPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load company logo for invoice");
        }
        return null;
    }

    /// <summary>
    /// Tries to load the image for an order item:
    ///   1. If SelectedImageId is set, look up the ProductImage → use its URL
    ///   2. Otherwise fall back to Product.ImageUrl (primary)
    ///   3. Read from wwwroot (local paths) - external URLs are skipped for PDF (they'd slow it down)
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

            // Defense-in-depth: ensure resolved path stays within WebRootPath
            if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_env.WebRootPath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Image path traversal blocked for OrderItem {OrderItemId}: {Path}", item.Id, imageUrl);
                return null;
            }

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
