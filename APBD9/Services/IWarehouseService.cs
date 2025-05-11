using APBD9.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace APBD9.Services;

public interface IWarehouseService
{
    public Task<int> HandleOrder(ProductWarehouseDTO productWarehouseDto);
    public Task<int> HandleOrderWithProcedure(ProductWarehouseDTO productWarehouseDto);
}