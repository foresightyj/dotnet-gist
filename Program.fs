// For more information see https://aka.ms/fsharp-console-apps
open System
open System.CommandLine
open Sharprompt
open Octokit
open Utils
open System.Threading.Tasks

let GET_GIST_GITHUB_TOKEN =
    let t = Environment.GetEnvironmentVariable("GET_GIST_GITHUB_TOKEN")

    if String.IsNullOrEmpty(t) then
        failwith "GET_GIST_GITHUB_TOKEN env is required"
    else
        t

let tokenAuth = Credentials(GET_GIST_GITHUB_TOKEN)
let github = GitHubClient(new ProductHeaderValue("dotnet-gist"))
github.Credentials <- tokenAuth

let displayAndCopyGistAsync url : Async<unit> =
    let url = new Uri(url)
    let id = url.PathAndQuery.Split('/') |> Seq.last

    async {
        let! res = github.Gist.Get(id) |> Async.AwaitTask

        let copied =
            res.Files.Values
            |> Seq.map (fun v -> sprintf "// file name: %s\r\n%s\r\n" v.Filename v.Content)
            |> String.concat "\r\n"

        do!
            TextCopy.ClipboardService.SetTextAsync(copied)
            |> Async.AwaitTask

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

type Command =
    | Help
    | List
    | Exit

type ListSubCommand =
    | ViewAndCopy
    | Edit
    | Delete
    | Rename

let getSubCommand =
    let urlOption = Option<string>("--url", "gist full url")
    urlOption.IsRequired <- true
    let get = Command("get", "get a gist by url")
    get.AddOption urlOption

    let handler url =
        async { do! displayAndCopyGistAsync (url) }

    let h = toLazyTask2 handler
    get.SetHandler(h, urlOption)
    get

let display (gist: Gist) =
    let copied =
        gist.Files.Values
        |> Seq.map (fun v -> sprintf "// file name: %s\r\n%s\r\n" v.Filename v.Content)
        |> String.concat "\r\n"

    TextCopy.ClipboardService.SetText(copied)
    copied |> printf "%s\r\n\r\n"

let replAsync () =
    let pickGistAsync () =
        async {
            let! gists = github.Gist.GetAll() |> Async.AwaitTask
            let selected = Prompt.Select("Pick a gist", gists, Nullable(), null, titleFor)
            return selected
        }

    let getUserInput () =
        let rawCmd = Prompt.Input<string>("Enter a command")
        fromStringIgnoreCase<Command> (rawCmd)

    let shouldExit =
        function
        | Some Exit -> true
        | _ -> false

    let deleteAsync (gist: Gist) =
        let answer = Prompt.Confirm($"Are you sure you want to delete %s{titleFor gist} ?")

        let t =
            if answer then
                github.Gist.Delete(gist.Id)
            else
                Task.CompletedTask

        t |> Async.AwaitTask

    let editGistAsync (gist: Gist) =
        async {
            let fileToEdit =
                Prompt.Select("Pick a file to edit", gist.Files, Nullable(), null, (fun i -> i.Value.Filename))

            let u = GistUpdate()
            u.Description <- gist.Description
            let! updated = github.Gist.Edit(gist.Id, u) |> Async.AwaitTask
            updated |> ignore
        }

    let renameGistAsync (gist: Gist) =
        async {
            let newName = Prompt.Input<string>($"Give %s{titleFor gist} a new name")
            let u = GistUpdate()
            u.Description <- newName
            let! updated = github.Gist.Edit(gist.Id, u) |> Async.AwaitTask
            updated |> ignore
        }

    let listAndThenAsync () : Async<unit> =
        let allSubCommands = [| ViewAndCopy; Delete; Rename |]

        async {
            let! gist = pickGistAsync ()

            let sub =
                Prompt.Select("Pick an action", allSubCommands, Nullable(), null, (fun c -> c.ToString()))

            do!
                match sub with
                | ViewAndCopy -> displayAndCopyGistAsync gist.HtmlUrl
                | Edit -> editGistAsync gist
                | Delete -> deleteAsync gist
                | Rename -> renameGistAsync gist
        }

    let rec runAsync (cmd: Command option) : Async<unit> =
        async {
            if shouldExit cmd then return ()

            let! cont =
                match cmd with
                | Some List ->
                    async {
                        do! listAndThenAsync ()
                        return true
                    }
                | Some Exit ->
                    async {
                        printf "Bye Bye!\r\n"
                        return false
                    }
                | Some Help ->
                    async {
                        let opts =
                            String.Join(
                                ", ",
                                unionToStrings<Command> ()
                                |> Array.map (fun n -> n.ToLower())
                            )

                        printf "Available commands: %s\r\n" opts
                        return true
                    }
                | None -> async { return true }

            if cont then
                let cmd = getUserInput ()
                do! runAsync cmd
            else
                ()
        }

    runAsync None

[<EntryPoint>]
let main args =
    // [How to define commands in System.CommandLine | Microsoft Learn]( https://learn.microsoft.com/en-us/dotnet/standard/commandline/define-commands )
    let root = RootCommand("Gist manager")
    root.Add(getSubCommand)

    root.SetHandler(toLazyTask replAsync)

    Prompt.ColorSchema.Select <- ConsoleColor.Magenta

    async {
        let t = root.InvokeAsync(args) |> Async.AwaitTask
        return! t
    }
    |> Async.RunSynchronously
