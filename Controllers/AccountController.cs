using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BackpropServer.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase {
        private readonly ILogger<AccountController> _logger;

        public AccountController(ILogger<AccountController> logger) {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<string> Accounts() {
            return "Accounts data goes here";
        }
    }
}
