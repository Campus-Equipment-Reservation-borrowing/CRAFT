using ASI.Basecode.Services.Mapping;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp
{
    // AutoMapper configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configure auto mapper
        /// </summary>
        private void ConfigureAutoMapper()
        {
            var mapperConfiguration = new MapperConfiguration(config =>
            {
                config.AddProfile(new MappingProfile());
            });

            this._services.AddSingleton<IMapper>(sp => mapperConfiguration.CreateMapper());
        }

    }
}
