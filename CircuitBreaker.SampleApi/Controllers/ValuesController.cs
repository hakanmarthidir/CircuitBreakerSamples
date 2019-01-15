using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBreaker.SampleApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public List<Student> Get()
        {            
            System.Threading.Thread.Sleep(10000);
            var studentList = new List<Student>() {
                new Student(){ Name = "A", Surname="B" },
                new Student(){ Name = "C", Surname="D" },
                new Student(){ Name = "E", Surname="F" }
            };
            return studentList;
        }        
    }
}
