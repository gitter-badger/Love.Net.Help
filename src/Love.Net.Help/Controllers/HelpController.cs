﻿// Copyright (c) rigofunc (xuyingting). All rights reserved.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Love.Net.Help.Controllers {
    /// <summary>
    /// Represents the help controller to generate API documentation.
    /// </summary>
    [Route("api/[controller]")]
    public class HelpController {
        private readonly IApiDescriptionGroupCollectionProvider _provider;
        private readonly ApiHelpOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpController"/> class.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="options">The api help options.</param>
        public HelpController(IApiDescriptionGroupCollectionProvider provider, IOptions<ApiHelpOptions> options) {
            _provider = provider;
            _options = options.Value;
        }

        [HttpGet]
        public JObject Get() {
            var json = new JObject();

            // all group
            var groupCollection = _provider.ApiDescriptionGroups.Items;

            // each group
            foreach (var group in groupCollection) {
                json.Add(group.GroupName, Handle(group));
            }

            return json;
        }

        [HttpGet("[action]")]
        public JObject Get([FromQuery]ApiInputModel model) {
            var items = _provider.ApiDescriptionGroups.Items.SelectMany(group => group.Items.Where(it => it.RelativePath == model.RelativePath && (model.HttpMethod == null ? true : it.HttpMethod == model.HttpMethod)));
            var json = new JObject();
            foreach (var item in items) {
                json.Add($"{item.HttpMethod} {item.RelativePath}", Handle(item));
            }

            return json;
        }

        private JToken Handle(ApiDescriptionGroup group) {
            if(_options.LoadingPolicy == LoadingPolicy.Lazy) {
                var array = new JArray();
                foreach (var item in group.Items) {
                    array.Add($"{item.HttpMethod} {item.RelativePath}");
                }
                return array;
            }
            else {
                var json = new JObject();
                foreach (var item in group.Items) {
                    json.Add($"{item.HttpMethod} {item.RelativePath}", Handle(item));
                }

                return json;
            }
        }

        private JToken Handle(ApiDescription item) {
            var json = new JObject();

            json.Add("Summary", GetActionSummary(item.ActionDescriptor));
            json.Add("Request", HandleRequest(item));
            json.Add("Response", HandleResponse(item));

            return json;
        }

        private JToken HandleRequest(ApiDescription item) {
            var json = new JObject();
            foreach (var parameter in item.ParameterDescriptions.Where(p => p.Source.IsFromRequest)) {
                json.Add(parameter.Name, HandlerParameter(parameter));
            }

            return json;
        }

        private JToken HandleResponse(ApiDescription item) {
            Type type = null;

            var action = item.ActionDescriptor;
            if (action is ControllerActionDescriptor) {
                var controllerAtion = action as ControllerActionDescriptor;
                // We only provide response info if we can figure out a type that is a user-data type.
                // Void /Task object/IActionResult will result in no data.
                type = GetDeclaredReturnType(controllerAtion);
            }
            else {
                var supportedResponseTypes = item.SupportedResponseTypes;
                if(supportedResponseTypes.Count > 0) {
                    type = supportedResponseTypes[0].Type;
                }
            }
            
            var json = new JObject();
            json.Add("Data", type.Scaffold());
            json.Add("Schema", type.Schema());

            return json;
        }

        private static JToken HandlerParameter(ApiParameterDescription parameter) {
            var json = new JObject();

            json.Add("Source", parameter.Source.Id);
            if (parameter.Type.IsPrimitive()) {
                json.Add("Data", parameter.Type.Scaffold());
            }
            else {
                json.Add("Data", parameter.Type.Scaffold());
                json.Add("Schema", parameter.Type.Schema());
            }

            return json;
        }

        private static string GetActionSummary(ActionDescriptor action) {
            if(action is ControllerActionDescriptor) {
                var controllerAtion = action as ControllerActionDescriptor;
                return controllerAtion.MethodInfo.XmlDoc();
            }
            else {
                return action.DisplayName;
            }
        }

        private static Type GetDeclaredReturnType(ControllerActionDescriptor action) {
            var declaredReturnType = action.MethodInfo.ReturnType;
            if (declaredReturnType == typeof(void) ||
                declaredReturnType == typeof(Task)) {
                return typeof(void);
            }

            // Unwrap the type if it's a Task<T>. The Task (non-generic) case was already handled.
            var unwrappedType = GetTaskInnerTypeOrNull(declaredReturnType) ?? declaredReturnType;

            // If the method is declared to return IActionResult or a derived class, that information
            // isn't valuable to the formatter.
            if (typeof(IActionResult).IsAssignableFrom(unwrappedType)) {
                return null;
            }
            else {
                return unwrappedType;
            }
        }

        private static Type GetTaskInnerTypeOrNull(Type type) {
            var genericType = ClosedGenericMatcher.ExtractGenericInterface(type, typeof(Task<>));

            return genericType?.GenericTypeArguments[0];
        }
    }
}
