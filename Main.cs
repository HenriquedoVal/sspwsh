///

using System;
using System.IO;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;


using Microsoft.PowerShell.Commands;


namespace SSPwshClient {


[CustomMarshaller(typeof(string),
        MarshalMode.Default,
        typeof(ConstCharPtrMarshaller))]
internal static unsafe class ConstCharPtrMarshaller
{
    public static string ConvertToManaged(IntPtr unmanaged)
    {
        return Utf8StringMarshaller.ConvertToManaged((byte*)unmanaged);
    }
}


/// ShellServer dll

static partial class SS
{
    const string dll_name = "nss_client.dll";

    // bool ss_add_refpath(const char *path, /*nullable*/ const char *as);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_add_refpath")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool add_refpath(string path, string _as);

    // bool ss_del_refpath(const char *refpath);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_del_refpath")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool del_refpath(string refpath);

    // bool ss_del_refpath_by_path(const char *path);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_del_refpath_by_path")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool del_refpath_by_path(string path);

    // bool ss_move_refpath_down(const char *path);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_move_refpath_down")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool move_refpath_down(string path);

    // bool ss_kill_server(void);
    [LibraryImport(dll_name, EntryPoint = "ss_kill_server")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool kill_server();

    // bool ss_save_cache(void);
    [LibraryImport(dll_name, EntryPoint = "ss_save_cache")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool save_cache();

    // void ss_shutdown(void);
    [LibraryImport(dll_name, EntryPoint = "ss_shutdown")]
    public static partial void shutdown();

    // const char *ss_get_prompt(const char *path,
    //                           short term_width,
    //                           int error_code,
    //                           unsigned cmd_dur_ms);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_get_prompt")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string get_prompt(
            string path, short term_width, nint error_code, nuint cmd_dur_ms);

    // const char *ss_echo(const char *msg);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_echo")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string echo(string msg);

    // const char *ss_get_path(const char *refpath);
    [LibraryImport(dll_name,
            StringMarshalling = StringMarshalling.Utf8,
            EntryPoint = "ss_get_path")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string get_path(string refpath);

    // const char *ss_get_all_refpaths(void);
    [LibraryImport(dll_name, EntryPoint = "ss_get_all_refpaths")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string get_all_refpaths();

    // const char *ss_get_cache_memory_state(void);
    [LibraryImport(dll_name, EntryPoint = "ss_get_cache_memory_state")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string get_cache_memory_state();

    // const char *ss_get_cache_stored_state(void);
    [LibraryImport(dll_name, EntryPoint = "ss_get_cache_stored_state")]
    [return: MarshalUsing(typeof(ConstCharPtrMarshaller))]
    public static partial string get_cache_stored_state();
}


// Probably doesn't work
class Cleanup : IModuleAssemblyCleanup
{
    public void OnRemove(PSModuleInfo mi) => SS.shutdown();
}


/// Cmdlets

[Cmdlet(VerbsCommon.Set, "ShellServerLocation")]
[OutputType(typeof(string))]
[Alias("p")]
public class SetLocation : PSCmdlet
{
    [Parameter(Position = 0)]
    [ArgumentCompleter(typeof(DirsAndRefPathsCompleter))]
    public string PathOrRefpath;

    [Parameter()]
    public SwitchParameter Output;

    [Parameter()]
    public SwitchParameter Junction;

    protected override void BeginProcessing()
    {
        string path = null;

        if (String.IsNullOrWhiteSpace(PathOrRefpath)) {
            if (Junction.IsPresent)
                path = SessionState.Path.CurrentLocation.Path;
            else
                path = Environment.GetEnvironmentVariable("USERPROFILE");
            goto path_set;
        }

        path = SS.get_path(PathOrRefpath);
        if (path is not null) goto path_set;

        if (Path.IsPathFullyQualified(PathOrRefpath))
            path = PathOrRefpath;
        if (Path.Exists(path)) goto path_set;

        path = Path.GetFullPath(
                Path.Combine(
                    SessionState.Path.CurrentLocation.Path,
                    PathOrRefpath));

        if (!Path.Exists(path)) throw new ArgumentException(
                $"{PathOrRefpath} is not a valid refpath, "
                + "relative path, or absolute path.");

        path_set:

        if (Junction.IsPresent) {
            var some = Directory.ResolveLinkTarget(path!, true);
            if (some is null)
                throw new ArgumentException(
                        $"{path} is not a junction");
            path = some.FullName;
        }

        if (Output.IsPresent) WriteObject(path);
        else SessionState.Path.SetLocation(path);
    }
}


// The "right" thing to do here would be to implement 'Add-ShellServerPath',
// 'Remove-ShellServerPath', 'Remove-ShellServerRefPath', or something like
// that, but I will go with `pe -a`, `pe -d`, `pe -dr`
[Cmdlet(VerbsData.Edit, "ShellServerCache")]
[OutputType(typeof(string))]
[Alias("pe")]
public class EditCache : PSCmdlet
{
    [Parameter()]
    [Alias("a")]
    public string Add;

    [Parameter()]
    public string As;

    [Parameter()]
    [Alias("d")]
    public string DelPath;

    [Parameter()]
    [Alias("dr")]
    [ArgumentCompleter(typeof(RefPathsCompleter))]
    public string DelRef;

    [Parameter()]
    [Alias("md")]
    [ArgumentCompleter(typeof(RefPathsCompleter))]
    public string MoveDown;

    string get_full_path(string what)
    {
        string full_path;

        if (Path.IsPathFullyQualified(what))
            full_path = what;
        else
            full_path = Path.Combine(SessionState.Path.CurrentLocation.Path, what);

        return full_path;
    }

    protected override void BeginProcessing()
    {
        if (Add is not null) {
            string full_path = get_full_path(Add);
            if (!SS.add_refpath(full_path, As))
                throw new Exception($"Could not add {full_path}");
        }

        if (DelPath is not null) {
            string full_path = get_full_path(DelPath);
            if (!SS.del_refpath_by_path(full_path))
                throw new Exception($"Could not delete path {full_path}");
        }

        if (DelRef is not null) {
            if (!SS.del_refpath(DelRef))
                throw new Exception($"Could not delete refpath {DelRef}");
        }

        if (MoveDown is not null) {
            if (!SS.move_refpath_down(MoveDown))
                throw new Exception($"Could not move refpath `{MoveDown}` down");
        }
    }
}


[Cmdlet(VerbsCommon.Show, "ShellServerCache")]
[OutputType(typeof(string))]
public class ShowCache : PSCmdlet
{
    [Parameter()]
    public SwitchParameter Stored;

    protected override void BeginProcessing()
    {
        string ret;

        if (Stored.IsPresent)
            ret = SS.get_cache_stored_state();
        else
            ret = SS.get_cache_memory_state();

        if (!String.IsNullOrWhiteSpace(ret))
            WriteObject(ret.Trim());
    }
}


[Cmdlet(VerbsData.Save, "ShellServerCache")]
public class SaveCache : PSCmdlet
{
    protected override void BeginProcessing()
    {
        if (!SS.save_cache())
            throw new ApplicationFailedException(
                    "Could not write cache to file system");
    }
}


[Cmdlet(VerbsLifecycle.Invoke, "ShellServerPrompt")]
[OutputType(typeof(string))]
[Alias("prompt")]
public class InvokePrompt : PSCmdlet
{
    protected override void BeginProcessing()
    {
        string path = SessionState.Path.CurrentLocation.Path;
        short width = Convert.ToInt16(Console.BufferWidth);

        nint lec = 0;
        bool was_ok = Convert.ToBoolean(GetVariableValue("?"));
        if (!was_ok) {
            lec = Convert.ToInt32(GetVariableValue("lastexitcode"));
            if (lec == 0) lec++;
        }

        nuint last_cmd_duration_ms = 0;
        var res = InvokeCommand.InvokeScript("Get-History -Count 1");
        if (res.Count > 0) {
            var hi = res[0].ImmediateBaseObject as HistoryInfo;
            last_cmd_duration_ms = (nuint)
                (hi!.EndExecutionTime - hi.StartExecutionTime).Milliseconds;
        }

        string ret = SS.get_prompt(path, width, lec, last_cmd_duration_ms);
        if (ret is null) {
            InvokeCommand.InvokeScript("Remove-Alias prompt");
            // throwing and WriteError has no effect here
            WriteObject("No response from server. Prompt reset.");
            return;
        }

        CompletionsHolder.UpdateCompletions();

        WriteObject(ret);
    }
}


/// Argument completer

class DirsAndRefPathsCompleter: IArgumentCompleter
{
    DirsCompleter Dirs = new DirsCompleter();
    RefPathsCompleter RefPaths = new RefPathsCompleter();

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        System.Collections.IDictionary fakeBoundParameters
    )
    {
        var refPathComps = RefPaths.CompleteArgument(
                    commandName, parameterName, wordToComplete, commandAst, fakeBoundParameters);
        foreach (CompletionResult comp in refPathComps) yield return comp;

        var dirs = Dirs.CompleteArgument(
                    commandName, parameterName, wordToComplete, commandAst, fakeBoundParameters);
        foreach (CompletionResult comp in dirs) yield return comp;
    }
}


static class CompletionsHolder
{
    public static string[] RefPathCompletions = {};

    public static void UpdateCompletions()
    {
        string ret = SS.get_all_refpaths();
        if (string.IsNullOrWhiteSpace(ret)) return;

        RefPathCompletions = ret.Split(';');
    }
}


class RefPathsCompleter: IArgumentCompleter
{

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        System.Collections.IDictionary fakeBoundParameters)
    {
        foreach (string comp in CompletionsHolder.RefPathCompletions) {

            if (string.IsNullOrEmpty(wordToComplete)
                    || comp.StartsWith(wordToComplete, StringComparison.InvariantCultureIgnoreCase)) {

                string res = comp.Contains(' ') ? $"'{comp}'" : comp;

                yield return new CompletionResult(
                        res, res,
                        CompletionResultType.ParameterValue,
                        "ShellServer path reference"
                );
            }
        }
    }
}


class DirsCompleter: IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName,
        string parameterName,
        string wordToComplete,
        CommandAst commandAst,
        System.Collections.IDictionary fakeBoundParameters
    )
    {
        var pathCompletions = CompletionCompleters.CompleteFilename(wordToComplete);

        foreach (var res in pathCompletions) {
            // ToolTip is the abs path.
            var attr = File.GetAttributes(res.ToolTip);
            bool isDir = (attr & FileAttributes.Directory) > 0;
            
            if (isDir) yield return res;
        }
    }
}


}  // namespace
