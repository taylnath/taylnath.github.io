#r "../_lib/Fornax.Core.dll"
#r "../_lib/Markdig.dll"
#r "nuget: FSharp.Data"

open Markdig
open System.Net.Http
open FSharp.Data

[<Literal>]
let ghUrl = "https://api.github.com/users/taylnath/repos"

type GHData = JsonProvider<ghUrl>

let repoData = GHData.Load(ghUrl)

type RepoData = JsonProvider<repoData.[0]>

type ProjectConfig = {
    disableLiveRefresh: bool
}
type PreProject = {
    name: string
    repo: string
}

type Project = {
    name: string
    repo: string
    desc: string
}

let contentDir = "projects"

let markdownPipeline =
    MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseGridTables()
        .Build()

let isSeparator (input : string) =
    input.StartsWith "---"

// not used
let isProjSeparator (input: string) =
    input.Contains "<!--proj-->"

let rec mapFindIndex' (pred: 'a -> bool) (index: int) (result: int list) (lis: 'a list) = 
    match lis with
        | [] -> result
        | (x::xs) -> 
            if (pred x) 
            then mapFindIndex' pred (index + 1) (result @ [index]) xs
            else mapFindIndex' pred (index + 1) result xs
 
// map the findIndex function over the array, i.e. find list of indices satisfying predicate
let mapFindIndex (pred: 'a -> bool) (arr: 'a array) = mapFindIndex' pred 0 [] (arr |> Array.toList)

// return list of slices of list, slicing on indices given
let rec cutOnFoundIndices (sliceList: int list) (result: 'a list list) (arr: 'a list) = 
    match sliceList with
        | [] -> result
        | [x] -> result @ [arr.[x..]]
        | (x::y::ys) -> cutOnFoundIndices ys (result @ [arr.[x..y]]) arr

// let preProjectToProject (preProjects: PreProject list) = 
//     async {
//         let client = new HttpClient()
//         let! response = client.GetAsync("https://api.github.com/users/taylnath/repos") |> Async.AwaitTask
//         response.EnsureSuccessStatusCode() |> ignore
//         let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
//         let! json = json.
//     }
//     0

///`fileContent` - content of page to parse. Usually whole content of `.md` file
///returns content of config that should be used for the page
let getContent (fileContent : string) =
    let fileContent = fileContent.Split '\n'
    let fileContent = fileContent |> Array.skip 1 //First line must be ---
    let indicesOfSeparators = fileContent |> mapFindIndex isSeparator
    // let indexOfSeperator = fileContent |> Array.findIndex isSeparator
    let splitKey (line: string) = 
        let separatorIndex = line.IndexOf(':')
        if separatorIndex > 0 then
            let key = line.[.. separatorIndex - 1].Trim().ToLower()
            let value = line.[separatorIndex + 1 ..].Trim() 
            Some(key, value)
        else 
            None
    fileContent
    |> Array.toList
    |> cutOnFoundIndices indicesOfSeparators []
    |> List.map (fun x -> List.choose splitKey x)
    |> List.map Map.ofList
    |> List.map (fun m -> {name = m.["name"]; repo = m.["repo"]; 
        desc = Option.defaultValue "None" repoData.[Array.findIndex (fun (x: Root array) -> x.Name == m.["name"]) repoData].Description})

let trimString (str : string) =
    str.Trim().TrimEnd('"').TrimStart('"')

let loadFile n =
    let text = System.IO.File.ReadAllText n

    let projects = getContent text

    let file = System.IO.Path.Combine(contentDir, (n |> System.IO.Path.GetFileNameWithoutExtension) + ".md").Replace("\\", "/")
    let link = "/" + System.IO.Path.Combine(contentDir, (n |> System.IO.Path.GetFileNameWithoutExtension) + ".html").Replace("\\", "/")

    let title = config |> Map.find "title" |> trimString
    let author = config |> Map.tryFind "author" |> Option.map trimString
    let published = config |> Map.tryFind "published" |> Option.map (trimString >> System.DateTime.Parse)

    let tags =
        let tagsOpt =
            config
            |> Map.tryFind "tags"
            |> Option.map (trimString >> fun n -> n.Split ',' |> Array.toList)
        defaultArg tagsOpt []

    { file = file
      link = link
      title = title
      author = author
      published = published
      tags = tags
      content = content
      summary = summary }

let loader (projectRoot: string) (siteContent: SiteContents) =
    let postsPath = System.IO.Path.Combine(projectRoot, contentDir)
    System.IO.Directory.GetFiles postsPath
    |> Array.filter (fun n -> n.EndsWith ".md")
    |> Array.map loadFile
    |> Array.iter (fun p -> siteContent.Add p)

    siteContent.Add({disableLiveRefresh = false})
    siteContent
