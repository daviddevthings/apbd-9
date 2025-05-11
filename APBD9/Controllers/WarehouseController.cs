using APBD9.Models.DTOs;
using APBD9.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD9.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarehouseController(IWarehouseService warehouseService) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> ManageWarehouse([FromBody] ProductWarehouseDTO? productWarehouseDto)
        {
            if (productWarehouseDto == null)
            {
                return BadRequest(new { message = "Body must not empty" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { message = "Invalid request", errors });
            }

            try
            {
                var idProductWarehouse = await warehouseService.HandleOrder(productWarehouseDto);
                return Ok(new { message = "Success", idProductWarehouse });
            }
            catch (ArgumentException e)
            {
                return BadRequest(new { error = e.Message });
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }

        [HttpPost("procedure")]
        public async Task<IActionResult> ManageWarehouseWithProcedure(
            [FromBody] ProductWarehouseDTO? productWarehouseDto)
        {
            if (productWarehouseDto == null)
            {
                return BadRequest(new { message = "Body is empty" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { message = "Invalid request", errors });
            }

            try
            {
                var idProductWarehouse = await warehouseService.HandleOrderWithProcedure(productWarehouseDto);
                return Ok(new { message = "Success", idProductWarehouse });
            }
            catch (ArgumentException e)
            {
                return BadRequest(new { message = e.Message });
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }
    }
}