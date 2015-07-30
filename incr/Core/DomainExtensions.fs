﻿namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage

open R4nd0mApps.TddStud10.Common.Domain
open System
open System.Threading

(* DELETE THIS ONCE WE MERGE BACK *)
module Common = 
    let safeExec (f : unit -> unit) = 
        try 
            f()
        with _ -> ()

(*Logger.logErrorf "Exception thrown: %s." (ex.ToString())*)
(* DELETE THIS ONCE WE MERGE BACK  *)

[<AutoOpen>]
module SynchronizationContextExtensions = 
    type SynchronizationContext with
        static member CaptureCurrent() = 
            match SynchronizationContext.Current with
            | null -> SynchronizationContext()
            | ctxt -> ctxt

[<AutoOpen>]
module DomainExtensions = 
    type Project with
        static member fromDTEProject (p : DTEProject) : Project = 
            { Id = 
                  { UniqueName = p.UniqueName
                    Id = p.ProjectGuid }
              Path = p.FullName |> FilePath
              Items = 
                  p.GetProjectItems()
                  |> Seq.filter DTEProjectItem.isSupported
                  |> Seq.map DTEProjectItem.getFiles
                  |> Seq.collect id
              FileReferences = p.GetFileReferences()
              ProjectReferences = p.GetProjectReferences() }
