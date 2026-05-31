import * as vscode from 'vscode'
import * as languageClient from './language-client'
import * as debuggerClient from './debugger-client'
import * as utils from './utils'

export let log: vscode.LogOutputChannel

export type GameUnit = {
    id: string,
    signal: string,
    source: string | null,
}

export function activate(context: vscode.ExtensionContext) {
    log = vscode.window.createOutputChannel("Nothingame Extension", { log: true })

    context.subscriptions.push(vscode.commands.registerCommand(`${utils.extensionId}.units.refresh`, () => {
        unitsTreeDataProvider.refresh()
    }))

    const unitsTreeDataProvider = new class implements vscode.TreeDataProvider<GameUnit> {
        constructor() { }

        private _onDidChangeTreeData = new vscode.EventEmitter<GameUnit | undefined>();
        readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

        refresh(): void {
            this._onDidChangeTreeData.fire(undefined)
        }

        getTreeItem(element: GameUnit): vscode.TreeItem {
            const res = new vscode.TreeItem(`${element.id} ${element.signal}`, vscode.TreeItemCollapsibleState.None)
            res.tooltip = element.source ?? 'No source'
            res.contextValue = `${utils.extensionId}Entity`
            if (element.source) {
                res.resourceUri = vscode.Uri.file(element.source)
                res.command = {
                    command: 'vscode.open',
                    title: 'Open File',
                    arguments: [element.source],
                }
            }
            return res
        }

        getChildren(element?: GameUnit): Thenable<GameUnit[]> {
            if (!element) {
                if (!languageClient.client) return Promise.resolve([])
                return languageClient.client.client.sendRequest('units')
            }
            return Promise.resolve([])
        }
    }()

    const treeView = vscode.window.createTreeView('gameUnits', {
        treeDataProvider: unitsTreeDataProvider,
    })
    context.subscriptions.push(treeView)

    languageClient.activate(context)
    debuggerClient.activate(context)
}

export function deactivate() {
    languageClient.deactivate()
    debuggerClient.deactivate()
}
