module Utils

open System
open System.Threading.Tasks
open Microsoft.FSharp.Reflection

// [toString and fromString for discriminated unions | F# Snippets]( http://www.fssnip.net/9l/title/toString-and-fromString-for-discriminated-unions )
let fromStringIgnoreCase<'a> (s: string) =
    match FSharpType.GetUnionCases typeof<'a>
          |> Array.filter (fun case -> case.Name.Equals(s, StringComparison.OrdinalIgnoreCase))
        with
    | [| case |] -> Some(FSharpValue.MakeUnion(case, [||]) :?> 'a)
    | _ -> None

let unionToStrings<'a> () =
    FSharpType.GetUnionCases typeof<'a>
    |> Array.map (fun case -> case.Name)


let awaitTask (t: Task) =
    t |> Async.AwaitIAsyncResult |> Async.Ignore

let toLazyTask2<'a> (asyncFn: 'a -> Async<unit>) : 'a -> Task =
    let fn (a: 'a) =
        let t = asyncFn a |> Async.StartAsTask
        t :> Task

    fn

let toLazyTask = toLazyTask2<unit>
