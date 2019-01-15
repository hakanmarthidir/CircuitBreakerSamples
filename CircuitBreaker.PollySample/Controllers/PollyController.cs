using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Polly;

namespace CircuitBreaker.PollySample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PollyController : ControllerBase
    {

        private readonly IHttpClientFactory httpClientFactory;

        public PollyController(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        //[HttpGet]
        //public async Task<IActionResult> GetAsync()
        //{
        //    var client = httpClientFactory.CreateClient(ClientNames.LocalClient);
        //    return Ok(await client.GetAsync("values"));
        //}

        [HttpGet]
        public List<StudentMap> Get()
        {
            var client = httpClientFactory.CreateClient(ClientNames.LocalClient);
            var response = client.GetAsync("values").Result;
            var studentList = response.Content.ReadAsAsync<List<StudentMap>>().Result;
            return studentList;
        }
    }
}