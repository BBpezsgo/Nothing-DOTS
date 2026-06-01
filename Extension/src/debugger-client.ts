'use strict'

import * as vscode from 'vscode'
import { WorkspaceFolder, DebugConfiguration, ProviderResult, CancellationToken } from 'vscode'
import * as fs from 'fs'
import { extensionId } from './utils'
import { GameUnit, log } from './extension'

export function activate(context: vscode.ExtensionContext) {
    log.debug('[Debugger] Activating debugger ...')

    let adapterFactory: vscode.DebugAdapterDescriptorFactory
    let trackerFactory: vscode.DebugAdapterTrackerFactory

    adapterFactory = new class implements vscode.DebugAdapterDescriptorFactory {
        createDebugAdapterDescriptor(session: vscode.DebugSession, executable: vscode.DebugAdapterExecutable | undefined): ProviderResult<vscode.DebugAdapterDescriptor> {
            log.trace(`[Debugger] Registering adapter descriptor`, session, executable)

            return new vscode.DebugAdapterServer(8053, '127.0.0.1')
        }
    }()

    trackerFactory = new class implements vscode.DebugAdapterTrackerFactory {
        createDebugAdapterTracker(session: vscode.DebugSession): ProviderResult<vscode.DebugAdapterTracker> {
            log.trace(`[Debugger] Registering adapter tracker`, session)
            return {
                onDidSendMessage(message: any): void {
                    log.trace(`[Debugger] <<`, message)
                },
                onWillReceiveMessage(message) {
                    log.trace(`[Debugger] >>`, message)
                },
                onWillStartSession() {
                    log.info(`[Debugger] Will start session`)
                },
                onWillStopSession() {
                    log.info(`[Debugger] Will stop session`)
                },
                onError(error: Error): void {
                    log.error(`[Debugger] Error`, error)
                },
                onExit(code, signal) {
                    log.debug(`[Debugger] Exit code: ${code} signal: ${signal}`)
                },
            }
        }
    }

    log.debug('[Debugger] Registering adapter descriptor factory')
    context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory(extensionId, adapterFactory))
    log.debug('[Debugger] Registering adapter tracker factory')
    context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(extensionId, trackerFactory))

    if ('dispose' in adapterFactory &&
        typeof adapterFactory.dispose === 'function' &&
        adapterFactory.dispose instanceof Function) {
        context.subscriptions.push(adapterFactory as { dispose(): void })
    }

    if ('dispose' in trackerFactory &&
        typeof trackerFactory.dispose === 'function' &&
        trackerFactory.dispose instanceof Function) {
        context.subscriptions.push(trackerFactory as { dispose(): void })
    }

    context.subscriptions.push(vscode.commands.registerCommand(`${extensionId}.debug.attach`, (entity: string | GameUnit) => {
        log.debug('[Debugger] Try to start debugging ...')

        if (!entity) {
            vscode.window.showErrorMessage(`No entity provided`)
            return
        }

        if (typeof entity === 'object') {
            entity = entity.id
        }

        log.trace('[Debugger] Entity:', entity)

        log.trace('[Debugger] Start debuging ...')
        vscode.debug.startDebugging(undefined, {
            type: extensionId,
            name: 'Debug Entity',
            request: 'attach',
            entity: entity,
        })
            .then(result => {
                if (!result) {
                    vscode.window.showErrorMessage('Failed to start debugging')
                    log.warn('[Debugger] Failed to start debugging')
                } else {
                    log.info('[Debugger] Debugging started')
                }
            }, error => {
                log.error(`[Debugger] Failed to start debugging`, error)
            })
    }))

    vscode.debug.onDidStartDebugSession(e => log.trace('[Debugger] Debug session started:', e))
    vscode.debug.onDidChangeActiveDebugSession(e => log.trace('[Debugger] Active debug session changed:', e))
    vscode.debug.onDidTerminateDebugSession(e => log.trace('[Debugger] Debug session terminated:', e))
    vscode.debug.onDidChangeBreakpoints(e => log.trace('[Debugger] Breakpoints changed:', e))

    const outputChannel = vscode.window.createOutputChannel("Nothingame Debug Host", { log: true })

    vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
        if (e.event === "adapterLog") {
            switch (e.body.level) {
                case 'trace': outputChannel.trace(e.body.message); break
                case 'debug': outputChannel.debug(e.body.message); break
                case 'info': outputChannel.info(e.body.message); break
                case 'warn': outputChannel.warn(e.body.message); break
                case 'error': outputChannel.error(e.body.message); break
                default: outputChannel.appendLine(e.body.message); break
            }
        } else {
            log.trace('[Debugger] Custom event received:', e)
        }
    })

    log.info('[Debugger] Activated')
}

export function deactivate() {

}

