using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdeWebLA09Models
{
    [SwaggerSchema("Művelet sikerességének osztálya")]
    public class ResultModel
    {
        [SwaggerSchema("Művelet sikeressége")]
        public bool Success { get; set; } = true;

        [SwaggerSchema("Művelet hibaüzenete (ha van hiba)")]
        public string ErrorMessage { get; set; } = String.Empty;
    }
}
