// For more information see https://aka.ms/fsharp-console-apps
open System
open Octokit

let GET_GIST_GITHUB_TOKEN =
    let t = Environment.GetEnvironmentVariable("GET_GIST_GITHUB_TOKEN")

    if String.IsNullOrEmpty(t) then
        failwith "GET_GIST_GITHUB_TOKEN env is required"
    else
        t

let tokenAuth = Credentials(GET_GIST_GITHUB_TOKEN)
let github = GitHubClient(new ProductHeaderValue("get-gist"))
github.Credentials <- tokenAuth

let getGistAsync url =
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

[<EntryPoint>]
let main args =
    args
    |> Array.tryHead
    |> function
        | Some arg -> Async.RunSynchronously(getGistAsync arg)
        | None -> failwith "Usage get-gist <gist-url>"

    0
