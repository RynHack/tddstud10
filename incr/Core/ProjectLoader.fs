﻿namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage

open R4nd0mApps.TddStud10.Common.Domain
open System
open System.Threading

type ProjectLoaderMessages = 
    | LoadProject of Solution * ProjectId * ProjectLoadResultMap

type ProjectLoader() = 
    let mutable disposed = false
    let onProjectLoaded = new Event<_>()
    let syncContext = SynchronizationContext.CaptureCurrent()
    
    let failedRes = 
        { Status = false
          Warnings = Seq.empty
          Errors = Seq.empty
          Outputs = Seq.empty }
    
    let processProject s p (rmap : ProjectLoadResultMap) = 
        let failedPrereqs = s.DependencyMap.[p] // TODO: WorkspaceAgent should not be handing off the map, just the list
                                                // Complication below is an indication of that.
                                                |> Seq.map (fun p -> p, rmap.[p].Value)
        if (failedPrereqs |> Seq.exists (fun (_, r) -> not r.Status)) then 
            { failedRes with Errors = 
                                 failedPrereqs 
                                 |> Seq.map (fun (p, _) -> sprintf "Required project %s failed to build." p.UniqueName) }
        else 
            let proj : Project option ref = ref None
            syncContext.Send((fun _ -> proj := (s, p) ||> ProjectExtensions.loadProject), null)
            match !proj with
            | Some proj -> 
                proj
                |> ProjectExtensions.createSnapshot s
                |> ProjectExtensions.fixupProject s.DependencyMap.[p] rmap
                |> ProjectExtensions.buildSnapshot
            | None -> { failedRes with Errors = [ sprintf "Required project %s failed to load." p.UniqueName ] }
    
    let rec processor (inbox : MailboxProcessor<_>) = 
        async { 
            let! msg = inbox.Receive()
            match msg with
            | LoadProject(s, p, rmap) -> 
                let res = 
                    try 
                        processProject s p rmap
                    with e -> 
                        { Status = false
                          Warnings = Seq.empty
                          Errors = [ e.ToString() ]
                          Outputs = Seq.empty }
                Common.safeExec (fun () -> onProjectLoaded.Trigger(p, res))
                return! processor inbox
        }
    
    let agent = AutoCancelAgent.Start(processor)
    
    let dispose disposing = 
        if not disposed then 
            if (disposing) then (agent :> IDisposable).Dispose()
            disposed <- true
    
    override __.Finalize() = dispose false
    
    interface IDisposable with
        member self.Dispose() : _ = 
            dispose true
            GC.SuppressFinalize(self)
    
    member __.LoadProject s p rmap = agent.Post((s, p, rmap) |> LoadProject)
    member __.OnProjectLoaded = onProjectLoaded.Publish
