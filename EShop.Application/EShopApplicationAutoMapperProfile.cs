using AutoMapper;
using EShop.Application.Products.Dtos;
using EShop.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace EShop.Application
{
    public class EShopApplicationAutoMapperProfile : Profile
    {
        public EShopApplicationAutoMapperProfile()
        {
            // 配置映射关系
            // 1. 从 CreateProductDto -> Product (创建时用)
            CreateMap<CreateProductDto, Product>();

            // 2. 从 Product -> ProductDto (查询时用)
            CreateMap<Product, ProductDto>();
        }
    }
    
}