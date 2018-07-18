using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Configuration;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService
{
    public class Mappings
    {
        private static bool _isInitialized;
        private static readonly object Locker = new object();

        // You can run issues where several tests fail if you Run-All tests but pass if you run each test individually.
        //  This can be caused by using Automapper and initializing Automapper from within multiple test classes.  When the
        //  tests are run in parallel, Automapper can step to itself due to being initialized more than once (once for each test class).
        //  The code below limits initialization to just one time.
        public static void CreateMappings()
        {
            lock (Locker)
            {
                if (!_isInitialized)
                {
                    Mapper.Initialize(CreateMappings);
                    _isInitialized = true;
                }
            }
        }

        private static void CreateMappings(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Synthesis.ProjectService.InternalApi.Models.Project, Synthesis.GuestService.InternalApi.Models.Project>()
                .ReverseMap();
        }
    }
}
