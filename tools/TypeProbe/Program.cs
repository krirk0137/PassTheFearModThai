// Prints real type/member names out of BepInEx interop assemblies, so plugin code is
// written against what the generator actually emitted rather than a guess at its
// namespace-prefix convention.
//
//   TypeProbe.exe <interopDir> <typeNameFilter> [memberFilter]

using System.Reflection;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: TypeProbe <interopDir> <typeFilter> [memberFilter]");
    return 1;
}

var dir = args[0];
var typeFilter = args[1];
var memberFilter = args.Length > 2 ? args[2] : null;

if (!Directory.Exists(dir))
{
    Console.Error.WriteLine($"no such directory: {dir}");
    return 1;
}

var probe = Directory.GetFiles(dir, "*.dll").ToList();
if (probe.Count == 0)
{
    Console.Error.WriteLine($"no .dll files under {dir}");
    return 1;
}

// MetadataLoadContext needs a core assembly in its resolver set; the interop folder has
// none, so add the running runtime's.
var runtime = Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll");
var resolver = new PathAssemblyResolver(probe.Concat(runtime).Distinct());
using var mlc = new MetadataLoadContext(resolver);

const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic
                              | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

foreach (var path in probe)
{
    Assembly asm;
    try { asm = mlc.LoadFromAssemblyPath(path); }
    catch { continue; }

    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }
    catch { continue; }

    foreach (var t in types)
    {
        // A handful of generated types have metadata this reflection-only context cannot
        // resolve; they throw from FullName or GetMembers. Skip them rather than abort.
        try
        {
            Dump(t);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"  (skipped a type in {Path.GetFileName(path)}: {e.GetType().Name})");
        }
    }

    void Dump(Type t)
    {
        var full = t.FullName ?? t.Name;
        if (!full.Contains(typeFilter, StringComparison.OrdinalIgnoreCase)) return;

        Console.WriteLine($"[{Path.GetFileName(path)}]  {(t.IsPublic ? "public" : "internal")} {full}");
        if (memberFilter is null) return;

        MemberInfo[] members;
        try { members = t.GetMembers(AllMembers); }
        catch { return; }

        foreach (var m in members)
        {
            if (!m.Name.Contains(memberFilter, StringComparison.OrdinalIgnoreCase)) continue;
            string sig;
            try
            {
                sig = m switch
                {
                    MethodInfo mi => $"{mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})",
                    PropertyInfo pi => $"{pi.PropertyType.Name} {pi.Name} {{ {(pi.CanRead ? "get; " : "")}{(pi.CanWrite ? "set; " : "")}}}",
                    FieldInfo fi => $"{fi.FieldType.Name} {fi.Name}",
                    _ => $"{m.MemberType} {m.Name}",
                };
            }
            catch { sig = $"{m.MemberType} {m.Name}  (signature unresolvable)"; }
            Console.WriteLine($"      {sig}");
        }
    }
}
return 0;
