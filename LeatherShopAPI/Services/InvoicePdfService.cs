using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Generates a vintage-styled PDF invoice inspired by classic 1940s British/French ledger invoices.
/// Uses QuestPDF (Community License) with warm sepia tones and ornamental borders.
/// </summary>
public class InvoicePdfService : IInvoicePdfService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<InvoicePdfService> _logger;

    // Vintage sepia color palette
    private const string Ink = "#2C1810";            // Dark sepia ink
    private const string InkMedium = "#5C3A1E";      // Medium brown
    private const string InkLight = "#8B6914";        // Antique gold/bronze
    private const string Parchment = "#FDF8F0";       // Warm cream background
    private const string ParchmentDark = "#F5E6D0";   // Darker parchment for rows
    private const string BorderOrnament = "#8B7355";  // Warm brown border
    private const string BorderLight = "#D4B896";     // Tan border
    private const string AccentGold = "#A0853C";      // Muted gold
    private const string StatusGreen = "#2D5016";     // Vintage green
    private const string StatusRed = "#8B1A1A";       // Vintage red
    private const string White = "#ffffff";

    // Decorative flourish characters
    private const string Flourish = "— ✦ —";
    private const string DoubleLine = "══════════════════════════════════════════════════════════════════════════";
    private const string SingleLine = "──────────────────────────────────────────────────────────────────────────";

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
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));

                // Full parchment background with ornamental border
                page.Background().Background(Parchment);

                page.Header().Element(c => ComposeHeader(c, order));
                page.Content().Element(c => ComposeContent(c, order));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    // ═══════════════════════ HEADER ═══════════════════════

    private void ComposeHeader(IContainer container, Order order)
    {
        container.PaddingHorizontal(35).PaddingTop(30).Column(col =>
        {
            // Outer ornamental border (top)
            col.Item().AlignCenter().Text(DoubleLine).FontSize(7).FontColor(BorderOrnament);

            col.Item().PaddingTop(12).Row(row =>
            {
                // Left: Company logo
                row.ConstantItem(90).Element(logoContainer =>
                {
                    var logoBytes = TryLoadLogo();
                    if (logoBytes != null)
                    {
                        logoContainer.Width(80).Height(80).Image(logoBytes);
                    }
                });

                // Center: Company branding
                row.RelativeItem().AlignCenter().Column(center =>
                {
                    center.Item().AlignCenter().Text("Est. 2024").FontSize(7).FontColor(InkLight).LetterSpacing(0.15f);
                    center.Item().PaddingTop(2).AlignCenter().Text("CUIR GALERIE").FontSize(24).Bold().FontColor(Ink).LetterSpacing(0.12f);
                    center.Item().PaddingTop(1).AlignCenter().Text("— Premium Handcrafted Leather Products —").FontSize(8).FontColor(InkMedium).LetterSpacing(0.08f);
                    center.Item().PaddingTop(6).AlignCenter().Text(Flourish).FontSize(10).FontColor(AccentGold);
                });

                // Right: Invoice number block
                row.ConstantItem(90).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("Nº").FontSize(7).FontColor(InkLight);
                    right.Item().AlignRight().Text(order.OrderNumber).FontSize(9).Bold().FontColor(Ink);
                });
            });

            // "INVOICE" title
            col.Item().PaddingTop(14).AlignCenter().Text("I N V O I C E").FontSize(16).Bold().FontColor(InkMedium).LetterSpacing(0.3f);

            col.Item().PaddingTop(4).AlignCenter().Text(SingleLine).FontSize(6).FontColor(BorderLight);

            // Two-column info
            col.Item().PaddingTop(14).Row(row =>
            {
                // Left: Bill To
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("B I L L   T O").FontSize(7).Bold().FontColor(InkLight).LetterSpacing(0.15f);
                    left.Item().PaddingTop(6).Text(string.IsNullOrEmpty(order.Customer?.Name) ? "Customer" : order.Customer.Name)
                        .FontSize(12).SemiBold().FontColor(Ink);
                    left.Item().PaddingTop(2).Text($"Tel: {order.Customer?.PhoneNumber ?? "—"}").FontSize(9).FontColor(InkMedium);
                    if (!string.IsNullOrWhiteSpace(order.ShippingAddress))
                        left.Item().PaddingTop(2).Text(order.ShippingAddress).FontSize(9).FontColor(InkMedium).LineHeight(1.4f);
                });

                // Right: Invoice details
                row.ConstantItem(190).Column(right =>
                {
                    right.Item().Text("D E T A I L S").FontSize(7).Bold().FontColor(InkLight).LetterSpacing(0.15f);

                    right.Item().PaddingTop(6).Element(c => ComposeDetailRow(c, "Date", order.CreatedAt.ToString("dd MMMM yyyy")));
                    right.Item().PaddingTop(3).Element(c => ComposeDetailRow(c, "Order", $"#{order.OrderNumber}"));
                    right.Item().PaddingTop(3).Element(c => ComposeDetailRow(c, "Status", order.Status.ToString(),
                        order.Status == OrderStatus.Cancelled ? StatusRed : Ink));
                    right.Item().PaddingTop(3).Element(c => ComposeDetailRow(c, "Payment", order.IsPaid ? "Paid" : "Unpaid",
                        order.IsPaid ? StatusGreen : StatusRed));
                });
            });

            col.Item().PaddingTop(16).AlignCenter().Text(DoubleLine).FontSize(7).FontColor(BorderOrnament);
        });
    }

    private static void ComposeDetailRow(IContainer container, string label, string value, string? color = null)
    {
        container.Row(row =>
        {
            row.ConstantItem(65).Text($"{label}:").FontSize(9).FontColor(InkMedium);
            row.RelativeItem().AlignRight().Text(value).FontSize(9).SemiBold().FontColor(color ?? Ink);
        });
    }

    // ═══════════════════════ CONTENT ═══════════════════════

    private void ComposeContent(IContainer container, Order order)
    {
        container.PaddingHorizontal(35).PaddingTop(14).Column(col =>
        {
            // Section label
            col.Item().PaddingBottom(8).Text("P A R T I C U L A R S").FontSize(7).Bold().FontColor(InkLight).LetterSpacing(0.15f);

            // Table header with vintage double-border look
            col.Item().BorderTop(2).BorderBottom(1).BorderColor(BorderOrnament).PaddingVertical(8).Row(headerRow =>
            {
                headerRow.ConstantItem(30).Text("Nº").SemiBold().FontSize(8).FontColor(InkMedium);
                headerRow.ConstantItem(48).Text("Image").SemiBold().FontSize(8).FontColor(InkMedium);
                headerRow.RelativeItem().Text("Description").SemiBold().FontSize(8).FontColor(InkMedium);
                headerRow.ConstantItem(35).AlignCenter().Text("Qty").SemiBold().FontSize(8).FontColor(InkMedium);
                headerRow.ConstantItem(75).AlignRight().Text("Rate").SemiBold().FontSize(8).FontColor(InkMedium);
                headerRow.ConstantItem(85).AlignRight().Text("Amount").SemiBold().FontSize(8).FontColor(InkMedium);
            });
            col.Item().BorderTop(1).BorderColor(BorderOrnament);

            // Table rows — alternating parchment shading (classic ledger)
            var index = 0;
            foreach (var item in order.OrderItems)
            {
                index++;
                var isEven = index % 2 == 0;
                var imageBytes = TryLoadImage(item);
                var subtotal = item.UnitPrice * item.Quantity;

                col.Item()
                    .Background(isEven ? ParchmentDark : Parchment)
                    .BorderBottom(1).BorderColor(BorderLight)
                    .PaddingVertical(6).Row(dataRow =>
                    {
                        // Row number
                        dataRow.ConstantItem(30).AlignMiddle()
                            .Text($"{index}.").FontSize(9).FontColor(InkMedium);

                        // Product image
                        dataRow.ConstantItem(48).AlignMiddle().Element(imgContainer =>
                        {
                            if (imageBytes != null)
                            {
                                imgContainer.Width(38).Height(38).Image(imageBytes);
                            }
                            else
                            {
                                imgContainer.Width(38).Height(38)
                                    .Background(ParchmentDark).Border(1).BorderColor(BorderLight)
                                    .AlignCenter().AlignMiddle()
                                    .Text("—").FontSize(12).FontColor(BorderLight);
                            }
                        });

                        // Product description
                        dataRow.RelativeItem().PaddingLeft(6).AlignMiddle()
                            .Text(item.Product?.Name ?? "Unknown Article").FontSize(10).FontColor(Ink);

                        // Quantity
                        dataRow.ConstantItem(35).AlignCenter().AlignMiddle()
                            .Text(item.Quantity.ToString()).FontSize(10).FontColor(Ink);

                        // Unit rate
                        dataRow.ConstantItem(75).AlignRight().AlignMiddle()
                            .Text($"₹{item.UnitPrice:N2}").FontSize(9).FontColor(InkMedium);

                        // Amount
                        dataRow.ConstantItem(85).AlignRight().AlignMiddle()
                            .Text($"₹{subtotal:N2}").FontSize(10).SemiBold().FontColor(Ink);
                    });
            }

            // Bottom border of items table
            col.Item().BorderTop(2).BorderColor(BorderOrnament);

            // Totals section — right-aligned, classic ledger style
            col.Item().PaddingTop(12).Row(totalRow =>
            {
                totalRow.RelativeItem(); // spacer

                totalRow.ConstantItem(220).Column(totalsCol =>
                {
                    // Subtotal
                    totalsCol.Item().PaddingVertical(4).Row(r =>
                    {
                        r.RelativeItem().Text("Subtotal").FontSize(10).FontColor(InkMedium);
                        r.ConstantItem(100).AlignRight().Text($"₹{order.TotalAmount:N2}").FontSize(10).FontColor(Ink);
                    });

                    // Ornamental divider before grand total
                    totalsCol.Item().PaddingVertical(3).BorderBottom(1).BorderColor(BorderOrnament);
                    totalsCol.Item().PaddingTop(1).BorderBottom(1).BorderColor(BorderOrnament);

                    // Grand total — prominent classic style
                    totalsCol.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Text("Total Amount Due").FontSize(12).Bold().FontColor(Ink);
                        r.ConstantItem(110).AlignRight().Text($"₹{order.TotalAmount:N2}").FontSize(14).Bold().FontColor(Ink);
                    });

                    // Double underline under total
                    totalsCol.Item().PaddingTop(4).BorderBottom(2).BorderColor(BorderOrnament);
                    totalsCol.Item().PaddingTop(2).BorderBottom(1).BorderColor(BorderOrnament);
                });
            });

            // Payment confirmation (vintage stamp-style)
            if (!string.IsNullOrEmpty(order.PaymentId) && order.IsPaid)
            {
                col.Item().PaddingTop(20).Row(r =>
                {
                    r.RelativeItem();
                    r.ConstantItem(180).Border(2).BorderColor(StatusGreen).Padding(10).Column(c =>
                    {
                        c.Item().AlignCenter().Text("✦  P A I D  ✦").FontSize(12).Bold().FontColor(StatusGreen).LetterSpacing(0.2f);
                        c.Item().PaddingTop(4).AlignCenter().Text($"Txn: {order.PaymentId}").FontSize(7).FontColor(InkMedium);
                    });
                });
            }
        });
    }

    // ═══════════════════════ FOOTER ═══════════════════════

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingHorizontal(35).PaddingBottom(25).Column(col =>
        {
            col.Item().AlignCenter().Text(DoubleLine).FontSize(7).FontColor(BorderOrnament);

            col.Item().PaddingTop(10).AlignCenter().Text("With Compliments").FontSize(10).FontColor(InkMedium);

            col.Item().PaddingTop(4).AlignCenter().Text(text =>
            {
                text.Span("CUIR GALERIE").FontSize(9).Bold().FontColor(Ink).LetterSpacing(0.1f);
                text.Span("  ·  Premium Handcrafted Leather Products").FontSize(8).FontColor(InkMedium);
            });

            col.Item().PaddingTop(6).AlignCenter().Text(Flourish).FontSize(9).FontColor(AccentGold);
        });
    }

    // ═══════════════════════ HELPERS ═══════════════════════

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
    /// Tries to load the image for an order item from wwwroot.
    /// Returns null if image can't be loaded (graceful degradation).
    /// </summary>
    private byte[]? TryLoadImage(OrderItem item)
    {
        try
        {
            string? imageUrl = null;

            if (item.SelectedImageId.HasValue && item.Product?.Images != null)
            {
                var selectedImg = item.Product.Images.FirstOrDefault(pi => pi.Id == item.SelectedImageId.Value);
                if (selectedImg != null)
                    imageUrl = selectedImg.ImageUrl;
            }

            if (string.IsNullOrEmpty(imageUrl))
                imageUrl = item.Product?.ImageUrl;

            if (string.IsNullOrEmpty(imageUrl))
                return null;

            if (imageUrl.StartsWith("http"))
            {
                _logger.LogDebug("Skipping external image URL for PDF: {Url}", imageUrl);
                return null;
            }

            var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));

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
