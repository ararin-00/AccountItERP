using AccountItERP.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountItERP.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            if (HttpContext.Session.GetString("Username") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.FullName = HttpContext.Session.GetString("FullName");

            var incomeQuery = _context.Incomes.AsQueryable();
            var expenseQuery = _context.Expenses.AsQueryable();
            var invoiceQuery = _context.Invoices.AsQueryable();
            var paymentQuery = _context.Payments.AsQueryable();

            if (fromDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived >= fromDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate >= fromDate.Value);
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= fromDate.Value);
                paymentQuery = paymentQuery.Where(p => p.PaymentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                incomeQuery = incomeQuery.Where(i => i.DateReceived <= toDate.Value);
                expenseQuery = expenseQuery.Where(e => e.ExpenseDate <= toDate.Value);
                invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= toDate.Value);
                paymentQuery = paymentQuery.Where(p => p.PaymentDate <= toDate.Value);
            }

            var totalIncome = await incomeQuery.SumAsync(i => i.Amount);
            var totalExpenses = await expenseQuery.SumAsync(e => e.Amount);
            var totalInvoices = await invoiceQuery.CountAsync();
            var totalPayments = await paymentQuery.SumAsync(p => p.AmountPaid);
            var netProfit = totalIncome - totalExpenses;

            var paidInvoices = await invoiceQuery.CountAsync(i => i.Status == "Paid");
            var pendingInvoices = await invoiceQuery.CountAsync(i => i.Status == "Pending");
            var cancelledInvoices = await invoiceQuery.CountAsync(i => i.Status == "Cancelled");

            var cashPayments = await paymentQuery
                .Where(p => p.PaymentMethod == "Cash")
                .SumAsync(p => p.AmountPaid);

            var bankPayments = await paymentQuery
                .Where(p => p.PaymentMethod == "Bank Transfer")
                .SumAsync(p => p.AmountPaid);

            var gcashPayments = await paymentQuery
                .Where(p => p.PaymentMethod == "GCash")
                .SumAsync(p => p.AmountPaid);

            var otherPayments = await paymentQuery
                .Where(p => p.PaymentMethod != "Cash"
                         && p.PaymentMethod != "Bank Transfer"
                         && p.PaymentMethod != "GCash")
                .SumAsync(p => p.AmountPaid);

            var incomeTransactions = await incomeQuery
                .Select(i => new DashboardTransaction
                {
                    Type = "Income",
                    Title = i.IncomeSource,
                    Description = i.Description,
                    Amount = i.Amount,
                    Date = i.DateReceived,
                    BadgeClass = "transaction-income",
                    IconClass = "bi bi-cash-stack"
                })
                .ToListAsync();

            var expenseTransactions = await expenseQuery
                .Select(e => new DashboardTransaction
                {
                    Type = "Expense",
                    Title = e.ExpenseCategory,
                    Description = e.Description,
                    Amount = e.Amount,
                    Date = e.ExpenseDate,
                    BadgeClass = "transaction-expense",
                    IconClass = "bi bi-wallet2"
                })
                .ToListAsync();

            var paymentTransactions = await paymentQuery
                .Include(p => p.Invoice)
                .Include(p => p.CustomerVendor)
                .Select(p => new DashboardTransaction
                {
                    Type = "Payment",
                    Title = p.Invoice != null ? p.Invoice.InvoiceNumber : "Payment Record",
                    Description = p.CustomerVendor != null ? p.CustomerVendor.Name : "Payment transaction",
                    Amount = p.AmountPaid,
                    Date = p.PaymentDate,
                    BadgeClass = "transaction-payment",
                    IconClass = "bi bi-credit-card-2-front"
                })
                .ToListAsync();

            var recentTransactions = incomeTransactions
                .Concat(expenseTransactions)
                .Concat(paymentTransactions)
                .OrderByDescending(t => t.Date)
                .Take(5)
                .ToList();

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.TotalInvoices = totalInvoices;
            ViewBag.TotalPayments = totalPayments;
            ViewBag.NetProfit = netProfit;

            ViewBag.PaidInvoices = paidInvoices;
            ViewBag.PendingInvoices = pendingInvoices;
            ViewBag.CancelledInvoices = cancelledInvoices;

            ViewBag.CashPayments = cashPayments;
            ViewBag.BankPayments = bankPayments;
            ViewBag.GCashPayments = gcashPayments;
            ViewBag.OtherPayments = otherPayments;

            ViewBag.RecentTransactions = recentTransactions;

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View();
        }
    }

    public class DashboardTransaction
    {
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string BadgeClass { get; set; } = "";
        public string IconClass { get; set; } = "";
    }
}