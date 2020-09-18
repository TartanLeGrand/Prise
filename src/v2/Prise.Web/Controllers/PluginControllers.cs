﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Prise.Console.Contract;

namespace Prise.Web.Controllers
{
    [ApiController]
    [Route("plugin")]
    /// <summary>
    /// This controller will have the IPlugin plugin injected automatically.
    /// Using this, there can only be 1 plugin registered and resolved from 1 directory
    /// You could replace the plugin in the directory with any other plugin at runtime, but there can still be only 1
    /// </summary>
    public class InjectionController : ControllerBase
    {
        private readonly IPlugin plugin;

        public InjectionController(IPlugin plugin)
        {
            this.plugin = plugin;
        }

        [HttpGet]
        public async Task<PluginObject> Get([FromQuery] string text)
        {
            return await this.plugin.GetData(new PluginObject
            {
                Number = new Random().Next(),
                Text = text
            });
        }
    }

    [ApiController]
    [Route("plugins")]
    /// <summary>
    /// This controller will have the IPlugin plugin injected automatically.
    /// Using this, there can only be 1 plugin registered and resolved from 1 directory
    /// You could replace the plugin in the directory with any other plugin at runtime, but there can still be only 1
    /// </summary>
    public class InjectionMultipleController : ControllerBase
    {
        private readonly IEnumerable<IMultiplePlugin> plugins;

        public InjectionMultipleController(IEnumerable<IMultiplePlugin> plugins)
        {
            this.plugins = plugins;
        }

        [HttpGet]
        public async Task<string> Get([FromQuery] string text)
        {
            var builder = new StringBuilder();
            
            foreach (var plugin in this.plugins)
                builder.AppendLine((await plugin.GetData(new PluginObject { Text = text })).Text);

            return builder.ToString();
        }
    }
}
