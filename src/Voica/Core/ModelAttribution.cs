using System.IO;
using System.Reflection;

namespace Voica;

/// <summary>Offline license and provenance for the pinned GigaAM model, including manual imports.</summary>
public static class ModelAttribution
{
    public static readonly string[] FileNames = { "GigaAM-LICENSE.txt", "GigaAM-MODEL.txt" };

    public static void WriteTo(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var name in FileNames)
        {
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Voica.Licenses." + name)
                ?? throw new InvalidOperationException("Missing embedded model attribution: " + name);
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            var bytes = buffer.ToArray();
            string target = Path.Combine(directory, name);
            if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes)) continue;

            string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, target, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
