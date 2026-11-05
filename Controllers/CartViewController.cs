using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class CartViewController : Controller
    {
        [Route("cart")]
        public IActionResult Index()
        {
            return View("~/Views/Cart/Index.cshtml");
        }
    }
}
