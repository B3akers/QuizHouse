﻿using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuizHouse.ActionFilters
{
    public class AdminActionFilterAttribute : ActionFilterAttribute
    {
        public AdminActionFilterAttribute()
        {
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {

        }
    }
}
