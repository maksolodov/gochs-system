using System.Text;
namespace Gochs.Common;
public static class AttachmentFiles
{
    public static bool Valid(string name,string type,byte[] bytes)
    {
        var ext=Path.GetExtension(name).ToLowerInvariant();
        if(ext==".pdf"&&type=="application/pdf"){
            var content=Encoding.Latin1.GetString(bytes);
            return content.StartsWith("%PDF-",StringComparison.Ordinal)&&content.TrimEnd().EndsWith("%%EOF",StringComparison.Ordinal);
        }
        if(ext is ".jpg" or ".jpeg"&&type=="image/jpeg")return bytes.Length>=4&&bytes[0]==255&&bytes[1]==216&&bytes[^2]==255&&bytes[^1]==217;
        if(ext==".png"&&type=="image/png")return bytes.Length>=45&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&Encoding.ASCII.GetString(bytes,12,4)=="IHDR"&&Encoding.ASCII.GetString(bytes,bytes.Length-8,4)=="IEND";
        return false;
    }
}
