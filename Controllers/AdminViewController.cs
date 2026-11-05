using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class AdminViewController : Controller
    {
        [Route("admin")]
        public IActionResult Index()
        {
            return View("~/Views/Admin/Index.cshtml");
        }

        [Route("warehouse")]
        public IActionResult Warehouse()
        {
            return View("~/Views/Admin/Warehouse.cshtml");
        }
    }
}
