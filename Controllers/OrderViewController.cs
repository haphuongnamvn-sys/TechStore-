using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class OrderViewController : Controller
    {
        [Route("orders")]
        [Route("order")]
        public IActionResult Index()
        {
            return View("~/Views/Order/Index.cshtml");
        }
    }
}
