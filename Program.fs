// For more information see https://aka.ms/fsharp-console-apps
open System
open System.CommandLine
open System.Threading.Tasks
open Sharprompt
open Octokit

let GET_GIST_GITHUB_TOKEN =
    let t = Environment.GetEnvironmentVariable("GET_GIST_GITHUB_TOKEN")

    if String.IsNullOrEmpty(t) then
        failwith "GET_GIST_GITHUB_TOKEN env is required"
    else
        t

let tokenAuth = Credentials(GET_GIST_GITHUB_TOKEN)
let github = GitHubClient(new ProductHeaderValue("dotnet-gist"))
github.Credentials <- tokenAuth

let getGistAsync url : Task =
    let url = new Uri(url)
    let id = url.PathAndQuery.Split('/') |> Seq.last

    task {
        let! res = github.Gist.Get(id)

        let copied =
            res.Files.Values
            |> Seq.map (fun v -> sprintf "// file name: %s\r\n%s\r\n" v.Filename v.Content)
            |> String.concat "\r\n"

        do! TextCopy.ClipboardService.SetTextAsync(copied)

        do copied |> printf "%s\n"
        Console.ForegroundColor <- ConsoleColor.Magenta
        Console.WriteLine("Gist copied to clipboard!")
        Console.ResetColor()
    }

let titleFor (gist: Gist) =
    if String.IsNullOrWhiteSpace(gist.Description) then
        $"[%s{gist.Id}]"
    else
        gist.Description

let pickGistAsync () =

    task {
        let! gists = github.Gist.GetAll()

        let selected = Prompt.Select("Pick a gist", gists, Nullable(), null, titleFor)

        return selected
    }


let listGistsAsync () : Task =
    task {
        let! selected = pickGistAsync ()
        do! getGistAsync selected.HtmlUrl
    }

let deleteGistAsync () : Task =
    task {
        let! selected = pickGistAsync ()

        let answer =
            Prompt.Confirm($"Are you sure you want to delete %s{titleFor selected} ?")

        if answer then
            do! github.Gist.Delete(selected.Id)
            $"Deleted %s{titleFor selected}" |> ignore
        else
            ()
    }

let renameGistAsync () : Task =
    task {
        let! selected = pickGistAsync ()
        let newName = Prompt.Input<string>("Give it a new name")
        let u = GistUpdate()
        u.Description <- newName
        let! updated = github.Gist.Edit(selected.Id, u)
        ()
    }

let getSubCommand =
    let urlOption = Option<string>("--url", "gist full url")
    urlOption.IsRequired <- true
    let get = Command("get", "get a gist by url")
    get.AddOption urlOption
    get.SetHandler(getGistAsync, urlOption)
    get

let listSubCommand =
    let list = Command("list", "list all gist")
    list.SetHandler(listGistsAsync)
    list

let deleteSubCommand =
    let delete = Command("delete", "delete a gist")
    delete.SetHandler(deleteGistAsync)
    delete

let renameSubCommand =
    let rename = Command("rename", "rename a gist")
    rename.SetHandler(renameGistAsync)
    rename


[<EntryPoint>]
let main args =
    // [How to define commands in System.CommandLine | Microsoft Learn]( https://learn.microsoft.com/en-us/dotnet/standard/commandline/define-commands )
    let root = RootCommand("Gist manager")
    root.Add(getSubCommand)
    root.Add(listSubCommand)
    root.Add(deleteSubCommand)
    root.Add(renameSubCommand)

    Prompt.ColorSchema.Select <- ConsoleColor.Magenta

    async {
        let t = root.InvokeAsync(args) |> Async.AwaitTask
        return! t
    }
    |> Async.RunSynchronously
