using core.Business;
using core.Interfaces;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace api.Controllers
{
    public class CloudAccessController : ApiController
    {
        IFileAccess _fileAccess = new FileAccess();

        [HttpGet]
        [ActionName("create-container")]
        public IEnumerable<string> CreateContainer()
        {
            _fileAccess.CreateContainer("containerzzcc");

            return new List<string>();
        }


    }
}
