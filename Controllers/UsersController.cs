using Microsoft.AspNetCore.Mvc;

namespace AccountItERP.Controllers
{
    public class UsersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}