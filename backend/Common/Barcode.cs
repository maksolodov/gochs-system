using System.Text;
namespace Gochs.Common;
public static class Barcode
{
    // Code 39, numeric internal card ID, narrow/wide ratio 1:3, quiet zones 10 modules.
    public static string Svg(int id)
    {
        string[] digits=["nnnwwnwnn","wnnwnnnnw","nnwwnnnnw","wnwwnnnnn","nnnwwnnnw","wnnwwnnnn","nnwwwnnnn","nnnwnnwnw","wnnwnnwnn","nnwwnnwnn"];
        var content="*"+id+"*";var bars=new StringBuilder();int x=10;
        foreach(var c in content){var pattern=c=='*'?"nwnnwnwnn":digits[c-'0'];for(int i=0;i<9;i++){int width=pattern[i]=='w'?3:1;if(i%2==0)bars.Append($"<rect x='{x}' y='10' width='{width}' height='50'/>");x+=width;}x++;}
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {x+10} 85' width='{(x+10)*3}' height='255'><rect width='100%' height='100%' fill='white'/><g fill='black'>{bars}</g><text x='{(x+10)/2}' y='78' font-family='sans-serif' font-size='11' text-anchor='middle'>СИЗ {id}</text></svg>";
    }
}
