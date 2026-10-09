using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace Gochs.Common;
public class OffsetTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        var value=reader.GetString();
        if(value==null||!Regex.IsMatch(value,@"(Z|[+-]\d{2}:\d{2})$",RegexOptions.CultureInvariant)||!DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out var result))
            throw new JsonException("Укажите дату и время с часовым поясом.");
        return result.ToUniversalTime();
    }
    public override void Write(Utf8JsonWriter writer,DateTimeOffset value,JsonSerializerOptions options)=>writer.WriteStringValue(value.ToUniversalTime());
}
