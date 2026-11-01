#!/usr/bin/env python3
"""Compile the Unity-dependent assemblies (Runtime, UI, Editor, PlayMode tests) with the dotnet SDK against the
managed DLLs of the installed Unity editor, without opening Unity. Uses the editor-generated Assembly-CSharp.csproj
as the template for reference paths and defines, so it tracks the real project configuration.

Usage: python3 -I Tools/unity_compile_check.py [project_root]
Exit code 0 = no errors. This is a compile check only; it does not run anything.
"""
import os, re, subprocess, sys, glob

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
TEMPLATE = os.path.join(ROOT, "Assembly-CSharp.csproj")
OUT_DIR = "/tmp/shadowsteps_unitycheck"
os.makedirs(OUT_DIR, exist_ok=True)
if not os.path.exists(TEMPLATE):
    print("Assembly-CSharp.csproj not found; open the project in Unity once so it generates csproj files.")
    sys.exit(2)

tpl = open(TEMPLATE).read()
# Drop the template's own compile items and project references; keep PropertyGroups and <Reference> items.
tpl = re.sub(r"\s*<Compile Include=\"[^\"]*\" />", "", tpl)
tpl = re.sub(r"<ProjectReference[\s\S]*?</ProjectReference>", "", tpl)
tpl = re.sub(r"<None Include=\"[^\"]*\" />", "", tpl)
tpl = tpl.replace("<AssemblyName>Assembly-CSharp</AssemblyName>", "<AssemblyName>ShadowSteps.UnityCheck</AssemblyName>")
tpl = re.sub(r"<OutputPath>[^<]*</OutputPath>", "<OutputPath>%s/bin/</OutputPath>" % OUT_DIR, tpl)
tpl = re.sub(r"<BaseIntermediateOutputPath>[^<]*</BaseIntermediateOutputPath>", "", tpl)
tpl = tpl.replace("<NoWarn>", "<NoWarn>CS0618;CS0649;CS0414;CS1701;CS1702;")

globs = [
    "Assets/_ShadowSteps/Scripts/Core/**/*.cs",
    "Assets/_ShadowSteps/Scripts/Data/**/*.cs",
    "Assets/_ShadowSteps/Scripts/Runtime/**/*.cs",
    "Assets/_ShadowSteps/Art3D/Scripts/**/*.cs",
    "Assets/_ShadowSteps/Art2D/Scripts/**/*.cs",
    "Assets/_ShadowSteps/Art2D/Tests/**/*.cs",
    "Assets/_ShadowSteps/Scripts/UI/**/*.cs",
    "Assets/_ShadowSteps/Scripts/Editor/**/*.cs",
    "Assets/_ShadowSteps/Tests/EditMode/**/*.cs",
    "Assets/_ShadowSteps/Tests/PlayMode/**/*.cs",
]
compile_items = "\n".join('    <Compile Include="%s" />' % os.path.join(ROOT, g) for g in globs)
extra_dlls = ["Unity.TextMeshPro.dll", "Unity.TextMeshPro.Editor.dll", "UnityEngine.UI.dll", "UnityEngine.TestRunner.dll", "UnityEditor.TestRunner.dll",
              "Unity.RenderPipelines.Universal.Runtime.dll", "Unity.RenderPipelines.Core.Runtime.dll", "Unity.RenderPipelines.Universal.Editor.dll", "Unity.RenderPipelines.Core.Editor.dll"]
refs = []
for dll in extra_dlls:
    p = os.path.join(ROOT, "Library", "ScriptAssemblies", dll)
    if os.path.exists(p):
        refs.append('    <Reference Include="%s"><HintPath>%s</HintPath></Reference>' % (dll[:-4], p))
nunit = glob.glob(os.path.join(ROOT, "Library/PackageCache/com.unity.ext.nunit@*/net35/unity-custom/nunit.framework.dll"))
if nunit:
    refs.append('    <Reference Include="nunit.framework"><HintPath>%s</HintPath></Reference>' % nunit[0])
items = "  <ItemGroup>\n%s\n%s\n  </ItemGroup>\n" % (compile_items, "\n".join(refs))
tpl = tpl.replace("</Project>", items + "</Project>")
csproj = os.path.join(OUT_DIR, "ShadowSteps.UnityCheck.csproj")
open(csproj, "w").write(tpl)
open(os.path.join(OUT_DIR, "Directory.Build.props"), "w").write(
    "<Project><PropertyGroup><BaseIntermediateOutputPath>%s/obj/</BaseIntermediateOutputPath>"
    "<MSBuildProjectExtensionsPath>%s/obj/</MSBuildProjectExtensionsPath></PropertyGroup></Project>" % (OUT_DIR, OUT_DIR))

cmd = ["dotnet", "build", csproj, "-nologo", "-v", "q", "-clp:NoSummary;ErrorsOnly", "-p:UseSharedCompilation=false"]
res = subprocess.run(cmd, capture_output=True, text=True)
out = (res.stdout + res.stderr)
errors = sorted(set(l.strip() for l in out.splitlines() if "error CS" in l))
for e in errors:
    # Make paths relative for readability.
    print(e.replace(ROOT + "/", ""))
print("unity compile check: %d error(s)" % len(errors))
sys.exit(0 if res.returncode == 0 and not errors else 1)
