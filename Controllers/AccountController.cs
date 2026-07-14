using AccountItERP.Data;
using AccountItERP.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AccountItERP.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("Username") != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                TempData["Error"] = "Invalid username or password.";
                return RedirectToAction(nameof(Login));
            }

            if (user.Status != "Active")
            {
                TempData["Error"] = user.Status switch
                {
                    "Pending" => "Your account is still waiting for admin approval.",
                    "Rejected" => "Your account registration was rejected.",
                    "Disabled" => "Your account has been disabled.",
                    _ => "Your account is not allowed to access the system."
                };

                return RedirectToAction(nameof(Login));
            }

            HttpContext.Session.SetInt32("UserID", user.UserID);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString(
                "FullName",
                $"{user.FirstName} {user.LastName}"
            );

            TempData["Success"] =
                $"Welcome back, {user.FirstName}!";

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Register()
        {
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> CheckUsername(string username)
        {
            username = username?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new
                {
                    available = false,
                    message = "Username is required."
                });
            }

            string normalizedUsername = username.ToLower();

            bool exists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == normalizedUsername);

            return Json(new
            {
                available = !exists,
                message = exists
                    ? "Username is already taken."
                    : "Username is available."
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmail(string email)
        {
            email = email?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new
                {
                    available = false,
                    message = "Email address is required."
                });
            }

            string normalizedEmail = email.ToLower();

            bool exists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            return Json(new
            {
                available = !exists,
                message = exists
                    ? "Email address is already registered."
                    : "Email address is available."
            });
        }

        [HttpGet]
public async Task<IActionResult> TemporaryResetUsers()
{
    var users = await _context.Users.ToListAsync();

    foreach (var user in users)
    {
        switch (user.Username.ToLower())
        {
            case "admin":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Admin123!");
                user.Status = "Active";
                break;

            case "accountant":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Accountant123!");
                user.Status = "Active";
                break;

            case "manager":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Manager123!");
                user.Status = "Active";
                break;

            case "staff":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Staff123!");
                user.Status = "Active";
                break;

            case "auditor":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Auditor123!");
                user.Status = "Active";
                break;

            case "msantos":
                user.Password = BCrypt.Net.BCrypt.HashPassword("Maria123!");
                user.Status = "Active";
                break;
        }
    }

    await _context.SaveChangesAsync();

    return Content("Passwords reset successfully.");
}





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            User user,
            string confirmPassword)
        {
            if (user.Password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View(user);
            }

            if (!IsStrongPassword(user.Password))
            {
                ViewBag.Error =
                    "Password must be at least 8 characters and include uppercase, lowercase, number, and special character, without spaces.";

                return View(user);
            }

            user.Username = user.Username.Trim();
            user.Email = user.Email.Trim();

            string normalizedUsername = user.Username.ToLower();
            string normalizedEmail = user.Email.ToLower();

            bool usernameExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == normalizedUsername);

            if (usernameExists)
            {
                ViewBag.Error = "Username is already taken.";
                return View(user);
            }

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                ViewBag.Error = "Email address is already registered.";
                return View(user);
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            user.Role = "Pending";
            user.Status = "Pending";

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Registration submitted successfully. Please wait for administrator approval.";

            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            TempData["Success"] =
                "You have been logged out successfully.";

            return RedirectToAction("Login");
        }

        private bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;

            return password.Length >= 8
                && Regex.IsMatch(password, "[A-Z]")
                && Regex.IsMatch(password, "[a-z]")
                && Regex.IsMatch(password, "[0-9]")
                && Regex.IsMatch(password, @"[\W_]")
                && !Regex.IsMatch(password, @"\s");
        }
    }
}