﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Registry.Web.Data;
using Registry.Web.Data.Models;
using Registry.Web.Models;
using Registry.Web.Models.DTO;
using Registry.Web.Services.Ports;
using Registry.Web.Utilities;

namespace Registry.Web.Controllers
{

    [Authorize]
    [ApiController]
    [Route(RoutesHelper.OrganizationsRadix + "/" + RoutesHelper.OrganizationSlug + "/" + RoutesHelper.DatasetRadix)]
    public class DatasetsController : ControllerBaseEx
    {
        private readonly IDatasetsManager _datasetsManager;
        private readonly IShareManager _shareManager;
        private readonly ILogger<DatasetsController> _logger;

        public DatasetsController(IDatasetsManager datasetsManager, IShareManager shareManager, ILogger<DatasetsController> logger)
        {
            _datasetsManager = datasetsManager;
            _shareManager = shareManager;
            _logger = logger;
        }


        [HttpGet(RoutesHelper.DatasetSlug + "/batches")]
        public async Task<IActionResult> Batches([FromRoute] string orgSlug, string dsSlug)
        {
            try
            {
                _logger.LogDebug($"Dataset controller Batches('{orgSlug}', '{dsSlug}')");

                var lst = await _shareManager.ListBatches(orgSlug, dsSlug);

                return Ok(lst);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Batches('{orgSlug}', '{dsSlug}')");

                return ExceptionResult(ex);
            }
        }

        [HttpGet(Name = nameof(DatasetsController) + "." + nameof(GetAll))]
        public async Task<IActionResult> GetAll([FromRoute] string orgSlug)
        {
            try
            {
                _logger.LogDebug($"Dataset controller GetAll('{orgSlug}')");
                return Ok(await _datasetsManager.List(orgSlug));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller GetAll('{orgSlug}')");
                return ExceptionResult(ex);
            }
        }

        [HttpGet(RoutesHelper.DatasetSlug, Name = nameof(DatasetsController) + "." + nameof(Get))]
        public async Task<IActionResult> Get([FromRoute] string orgSlug, string dsSlug)
        {
            try
            {
                _logger.LogDebug($"Dataset controller Get('{orgSlug}', '{dsSlug}')");

                return Ok(await _datasetsManager.GetEntry(orgSlug, dsSlug));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Get('{orgSlug}', '{dsSlug}')");
                return ExceptionResult(ex);
            }
        }

        [HttpGet(RoutesHelper.DatasetSlug + "/ex", Name = nameof(DatasetsController) + "." + nameof(GetEx))]
        public async Task<IActionResult> GetEx([FromRoute] string orgSlug, string dsSlug)
        {
            try
            {
                _logger.LogDebug($"Dataset controller GetEx('{orgSlug}', '{dsSlug}')");

                return Ok(await _datasetsManager.Get(orgSlug, dsSlug));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller GetEx('{orgSlug}', '{dsSlug}')");
                return ExceptionResult(ex);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromRoute] string orgSlug, [FromForm] DatasetDto dataset)
        {
            try
            {
                _logger.LogDebug($"Dataset controller Post('{orgSlug}', '{dataset?.Slug}')");

                var newDs = await _datasetsManager.AddNew(orgSlug, dataset);
                return CreatedAtRoute(nameof(DatasetsController) + "." + nameof(Get), new { dsSlug = newDs.Slug },
                    newDs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Post('{orgSlug}', '{dataset?.Slug}')");
                return ExceptionResult(ex);
            }
        }

        [HttpPost(RoutesHelper.DatasetSlug + "/rename")]
        public async Task<IActionResult> Rename([FromRoute] string orgSlug, string dsSlug, [FromForm(Name = "slug")] string newSlug)
        {
            try
            {
                _logger.LogDebug($"Dataset controller Rename('{orgSlug}', '{dsSlug}', '{newSlug}')");

                await _datasetsManager.Rename(orgSlug, dsSlug, newSlug);

                return Ok(await _datasetsManager.Get(orgSlug, newSlug));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Rename('{orgSlug}', '{dsSlug}', '{newSlug}')");
                return ExceptionResult(ex);
            }
        }

        [HttpPost(RoutesHelper.DatasetSlug + "/chattr")]
        public async Task<IActionResult> ChangeAttributes([FromRoute] string orgSlug, string dsSlug, [FromForm(Name = "attrs")] Dictionary<string, object> attributes)
        {
            try
            {
                _logger.LogDebug($"Dataset controller ChangeAttributes('{orgSlug}', '{dsSlug}', '{(attributes != null ? JsonConvert.SerializeObject(attributes) : null)}')");

                var res = await _datasetsManager.ChangeAttributes(orgSlug, dsSlug, attributes);

                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller ChangeAttributes('{orgSlug}', '{dsSlug}', '{(attributes != null ? JsonConvert.SerializeObject(attributes) : null)}')')");
                return ExceptionResult(ex);
            }
        }

        [HttpPut(RoutesHelper.DatasetSlug)]
        public async Task<IActionResult> Put([FromRoute] string orgSlug, string dsSlug, [FromForm] DatasetDto dataset)
        {

            try
            {
                _logger.LogDebug($"Dataset controller Put('{orgSlug}', '{dsSlug}', '{dataset?.Slug}')");

                await _datasetsManager.Edit(orgSlug, dsSlug, dataset);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Put('{orgSlug}', '{dsSlug}', '{dataset?.Slug}')");
                return ExceptionResult(ex);
            }

        }

        [HttpDelete(RoutesHelper.DatasetSlug)]
        public async Task<IActionResult> Delete([FromRoute] string orgSlug, string dsSlug)
        {

            try
            {
                _logger.LogDebug($"Dataset controller Delete('{orgSlug}', '{dsSlug}')");

                await _datasetsManager.Delete(orgSlug, dsSlug);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in Dataset controller Delete('{orgSlug}', '{dsSlug}')");

                return ExceptionResult(ex);
            }

        }


    }
}
