using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    public class SupportViewController : Controller
    {
        [Route("support")]
        public IActionResult Index()
        {
            return View("~/Views/Support/Index.cshtml");
        }
    }
}
