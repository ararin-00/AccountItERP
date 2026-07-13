using AccountItERP.Data;
using AccountItERP.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AccountItERP.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CanAccessReports()
        {
            var role = HttpContext.Session.GetString("Role");
            return RoleAccess.HasAccess(role, "Administrator", "Accountant", "Staff", "Manager");
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            if (!CanAccessReports())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var incomeQuery = _context.Incomes.AsQueryable();
            var expenseQuery = _context.Expenses.AsQueryable();

            if (fromDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived >= fromDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived <= toDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate <= toDate.Value);
            }

            var totalIncome = await incomeQuery.SumAsync(i => i.Amount);
            var totalExpenses = await expenseQuery.SumAsync(e => e.Amount);
            var netProfit = totalIncome - totalExpenses;

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.NetProfit = netProfit;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View();
        }

        public async Task<IActionResult> ExportPdf(DateTime? fromDate, DateTime? toDate)
        {
            if (!CanAccessReports())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            QuestPDF.Settings.License = LicenseType.Community;

            var incomeQuery = _context.Incomes.AsQueryable();
            var expenseQuery = _context.Expenses.AsQueryable();

            if (fromDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived >= fromDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived <= toDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate <= toDate.Value);
            }

            var incomes = await incomeQuery.OrderBy(i => i.DateReceived).ToListAsync();
            var expenses = await expenseQuery.OrderBy(e => e.ExpenseDate).ToListAsync();

            var totalIncome = incomes.Sum(i => i.Amount);
            var totalExpenses = expenses.Sum(e => e.Amount);
            var netProfit = totalIncome - totalExpenses;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(header =>
                    {
                        header.Item().Text("AccountItERP")
                            .FontSize(22)
                            .Bold()
                            .FontColor(Colors.Blue.Medium);

                        header.Item().Text("Profit & Loss Report")
                            .FontSize(16)
                            .Bold();

                        header.Item().Text($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        header.Item().Text($"Period: {(fromDate.HasValue ? fromDate.Value.ToString("MMM dd, yyyy") : "Start")} - {(toDate.HasValue ? toDate.Value.ToString("MMM dd, yyyy") : "End")}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingVertical(20).Column(content =>
                    {
                        content.Spacing(14);

                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Green.Lighten5).Padding(12).Column(col =>
                            {
                                col.Item().Text("Total Income").Bold().FontColor(Colors.Green.Darken2);
                                col.Item().Text($"PHP {totalIncome:N2}").FontSize(15).Bold();
                            });

                            row.RelativeItem().Background(Colors.Red.Lighten5).Padding(12).Column(col =>
                            {
                                col.Item().Text("Total Expenses").Bold().FontColor(Colors.Red.Darken2);
                                col.Item().Text($"PHP {totalExpenses:N2}").FontSize(15).Bold();
                            });

                            row.RelativeItem().Background(Colors.Blue.Lighten5).Padding(12).Column(col =>
                            {
                                col.Item().Text("Net Profit / Loss").Bold().FontColor(Colors.Blue.Darken2);
                                col.Item().Text($"PHP {netProfit:N2}").FontSize(15).Bold();
                            });
                        });

                        content.Item().Text("Income Records").FontSize(14).Bold();

                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeader).Text("Source");
                                header.Cell().Element(CellHeader).Text("Amount");
                                header.Cell().Element(CellHeader).Text("Date");
                                header.Cell().Element(CellHeader).Text("Description");
                            });

                            foreach (var income in incomes)
                            {
                                table.Cell().Element(CellBody).Text(income.IncomeSource);
                                table.Cell().Element(CellBody).Text($"PHP {income.Amount:N2}");
                                table.Cell().Element(CellBody).Text(income.DateReceived.ToString("MMM dd, yyyy"));
                                table.Cell().Element(CellBody).Text(income.Description ?? "-");
                            }
                        });

                        content.Item().Text("Expense Records").FontSize(14).Bold();

                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeader).Text("Category");
                                header.Cell().Element(CellHeader).Text("Amount");
                                header.Cell().Element(CellHeader).Text("Date");
                                header.Cell().Element(CellHeader).Text("Description");
                            });

                            foreach (var expense in expenses)
                            {
                                table.Cell().Element(CellBody).Text(expense.ExpenseCategory);
                                table.Cell().Element(CellBody).Text($"PHP {expense.Amount:N2}");
                                table.Cell().Element(CellBody).Text(expense.ExpenseDate.ToString("MMM dd, yyyy"));
                                table.Cell().Element(CellBody).Text(expense.Description ?? "-");
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("AccountItERP - Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", "Profit-Loss-Report.pdf");
        }

        private static IContainer CellHeader(IContainer container)
        {
            return container
                .Background(Colors.Blue.Lighten4)
                .Padding(6)
                .DefaultTextStyle(x => x.Bold());
        }

        private static IContainer CellBody(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(6);
        }
    }
}