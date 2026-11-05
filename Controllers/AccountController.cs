using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class AccountController : Controller
    {
        [Route("account/login")]
        public IActionResult Login()
        {
            return View();
        }

        [Route("account/register")]
        public IActionResult Register()
        {
            return View();
        }

        [Route("account/profile")]
        public IActionResult Profile()
        {
            return View();
        }
    }
}
