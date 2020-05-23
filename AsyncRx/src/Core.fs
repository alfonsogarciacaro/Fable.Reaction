namespace FSharp.Control

open System.Threading

module Core =
    let infinite = Seq.initInfinite id

    /// Safe observer that wraps the given observer. Makes sure that
    /// invocations are serialized and that the Rx grammar (OnNext*
    /// (OnError|OnCompleted)?) is not violated.
    let safeObserver (obv: IAsyncObserver<'TSource>) : IAsyncObserver<'TSource> =
        let agent = MailboxProcessor.Start (fun inbox ->
            let rec messageLoop stopped = async {
                let! n = inbox.Receive ()

                if stopped then
                    return! messageLoop stopped

                let! stop = async {
                    match n with
                    | OnNext x ->
                        try
                            do! obv.OnNextAsync x
                            return false
                        with
                        | ex ->
                            do! obv.OnErrorAsync ex
                            return true
                    | OnError ex ->
                        do! obv.OnErrorAsync ex
                        return true
                    | OnCompleted ->
                        do! obv.OnCompletedAsync ()
                        return true
                }

                return! messageLoop stop
            }
            messageLoop false)
        { new IAsyncObserver<'TSource> with
            member this.OnNextAsync x = async {
                OnNext x |> agent.Post
            }
            member this.OnErrorAsync err = async {
                OnError err |> agent.Post
            }
            member this.OnCompletedAsync () = async {
                OnCompleted  |> agent.Post
            }
        }

    type Async with
        /// Starts the asynchronous computation in the thread pool, or
        /// immediately for Fable. Do not await its result. If no cancellation
        /// token is provided then the default cancellation token is used.
        static member Start' (computation:Async<unit>, ?cancellationToken: CancellationToken) : unit =
            #if FABLE_COMPILER
                Async.StartImmediate (computation, ?cancellationToken=cancellationToken)
            #else
                Async.Start (computation, ?cancellationToken=cancellationToken)
            #endif

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]

    module Async =
        let empty = async { () }

        let noop = fun _ -> empty
