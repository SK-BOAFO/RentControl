using AutoMapper;
using RentControlSystem.Auth.API.DTOs;
using RentControlSystem.Auth.API.Models;
using System.Collections.Generic;

namespace RentControlSystem.Auth.API.Services
{
    public interface IMapperService
    {
        TDestination Map<TDestination>(object source);
        TDestination Map<TSource, TDestination>(TSource source);
        List<TDestination> MapList<TSource, TDestination>(List<TSource> source);
    }

    public class MapperService : IMapperService
    {
        private readonly IMapper _mapper;

        public MapperService()
        {
            // Try without AssertConfigurationIsValid
            var configuration = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<ApplicationUser, UserResponseDto>()
                   .ForMember(dest => dest.Roles, opt => opt.Ignore())
                   .ForMember(dest => dest.Profile, opt => opt.MapFrom(src => src.Profile));

                cfg.CreateMap<UserProfile, UserProfileDto>();

                cfg.CreateMap<ApplicationUser, AuthResponseDto>()
                   .ForMember(dest => dest.Roles, opt => opt.Ignore());
            });

            _mapper = configuration.CreateMapper();
        }

        public TDestination Map<TDestination>(object source)
        {
            return _mapper.Map<TDestination>(source);
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            return _mapper.Map<TSource, TDestination>(source);
        }

        public List<TDestination> MapList<TSource, TDestination>(List<TSource> source)
        {
            return _mapper.Map<List<TSource>, List<TDestination>>(source);
        }
    }
}