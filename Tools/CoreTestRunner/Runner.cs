using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>Minimal NUnit attribute runner: supports [Test], [TestCase], [SetUp], [TearDown], [Ignore]. Exit code 1 on failure.</summary>
public static class Runner
{
    public static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : null;
        var asm = Assembly.GetExecutingAssembly();
        int passed = 0, failed = 0, skipped = 0;
        var failures = new List<string>();
        foreach (var type in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith("ShadowSteps.Tests")).OrderBy(t => t.FullName))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes<TestAttribute>().Any() || m.GetCustomAttributes<TestCaseAttribute>().Any()).ToList();
            if (methods.Count == 0) continue;
            var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes<SetUpAttribute>().Any());
            var tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes<TearDownAttribute>().Any());
            foreach (var m in methods.OrderBy(m => m.Name))
            {
                var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(tc => tc.Arguments).ToList();
                if (cases.Count == 0) cases.Add(new object[0]);
                foreach (var caseArgs in cases)
                {
                    string name = type.Name + "." + m.Name + (caseArgs.Length > 0 ? "(" + string.Join(",", caseArgs.Select(a => a is Array arr ? "[" + string.Join(",", arr.Cast<object>()) + "]" : a?.ToString())) + ")" : "");
                    if (filter != null && !name.Contains(filter)) continue;
                    if (m.GetCustomAttributes<IgnoreAttribute>().Any()) { skipped++; continue; }
                    object instance = Activator.CreateInstance(type);
                    // Fresh NUnit ad-hoc context per test so recorded assertion failures do not bleed between tests.
                    NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentResult.AssertionResults.Clear();
                    try
                    {
                        setUp?.Invoke(instance, null);
                        var ps = m.GetParameters();
                        var callArgs = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++) callArgs[i] = Convert(caseArgs[i], ps[i].ParameterType);
                        m.Invoke(instance, callArgs);
                        passed++;
                        var output = NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentResult.Output;
                        if (!string.IsNullOrEmpty(output)) Console.WriteLine("OUTPUT " + name + ":\n" + output);
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        var e = tie.InnerException ?? tie;
                        failures.Add("FAIL " + name + "\n    " + e.GetType().Name + ": " + e.Message.Replace("\n", "\n    "));
                        Console.WriteLine("FAIL " + name);
                    }
                    finally
                    {
                        try { tearDown?.Invoke(instance, null); } catch (Exception e) { Console.WriteLine("TEARDOWN ERROR " + name + ": " + e.Message); }
                    }
                }
            }
        }
        Console.WriteLine();
        foreach (var f in failures) Console.WriteLine(f + "\n");
        Console.WriteLine("passed=" + passed + " failed=" + failed + " skipped=" + skipped);
        return failed == 0 ? 0 : 1;
    }

    private static object Convert(object value, Type target)
    {
        if (value == null) return null;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return Enum.ToObject(target, value);
        if (target.IsArray && value is Array src)
        {
            var et = target.GetElementType();
            var dst = Array.CreateInstance(et, src.Length);
            for (int i = 0; i < src.Length; i++) dst.SetValue(Convert(src.GetValue(i), et), i);
            return dst;
        }
        return System.Convert.ChangeType(value, target);
    }
}
