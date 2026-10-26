using System.Text;
using log4net.Core;
using log4net.Layout.Pattern;

namespace MyApp.Bootstrap.Logging;

/// <summary>
/// <c>%safemessage</c> en los layouts de <c>log4net.config</c>: el mensaje renderizado, con los saltos
/// de línea escapados. Todo log de la app (módulos y framework) termina en log4net (Program.cs hace
/// <c>ClearProviders()</c> + <c>AddLog4Net</c>), así que este es el único punto donde el saneamiento
/// aplica a todo el pipeline sin tocar ninguna llamada a <c>ILogger</c>.
/// </summary>
/// <remarks>
/// Evita la inyección de líneas de log (hallazgo 4.4 del ROADMAP): un valor de entrada como
/// <c>"x\nERROR [admin] Security - login ok"</c> quedaría, con <c>%message</c>, como una segunda línea
/// indistinguible de un evento real. Con <c>%safemessage</c> queda en la misma línea, con un <c>\n</c>
/// literal visible. No toca <c>%exception</c>: el stack trace es multilínea a propósito.
/// </remarks>
public sealed class SafeMessagePatternConverter : PatternLayoutConverter
{
    protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
    {
        writer.Write(Sanitize(loggingEvent.RenderedMessage));
    }

    /// <summary>
    /// Reemplaza <c>\r</c> y <c>\n</c> por los literales <c>\r</c> y <c>\n</c>, y los demás separadores
    /// de línea Unicode (NEL, LS, PS), que algunos visores también muestran como salto, por su escape
    /// <c>\uXXXX</c>.
    /// </summary>
    public static string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message) || message.AsSpan().IndexOfAny("\r\n\u0085\u2028\u2029") < 0)
        {
            return message ?? string.Empty;
        }

        var builder = new StringBuilder(message.Length + 8);
        foreach (var character in message)
        {
            switch (character)
            {
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                case '\u0085': builder.Append("\\u0085"); break;
                case '\u2028': builder.Append("\\u2028"); break;
                case '\u2029': builder.Append("\\u2029"); break;
                default: builder.Append(character); break;
            }
        }

        return builder.ToString();
    }
}
