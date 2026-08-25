# Building a C# Repo from GitHub / How to download and run the validator

This document describes how to download and run the MathML validator. 

## Some information for "non-technical" people that might be helpful

The MathML validator is a program that needs to be built before it can be run. A script, on the other hand, can be run right away. Whenever the validator is updated and you pull the latest changes, it must be rebuilt. This document decribes the steps you need to follow to build and run the validator.

## Prerequisites

Dotnet: You can download it from here: <https://dotnet.microsoft.com/download>. To check what version you have, run `dotnet --version` in the terminal.

## Step 1: Download or clone the validator

To get the latest version of the validator, you can choose to download the zip-file from GitHub, or pull the latest changes to your cloned version of the repository.

## Step 2: Build the validator

### Option A — Command line

```bash
dotnet restore   # downloads NuGet packages
dotnet build     # compiles
```

`dotnet build` restores automatically, so `restore` is usually optional.

**Useful variations:**

```bash
dotnet build -c Release          # optimised build
dotnet run --project src/App     # build and launch
dotnet test                      # run the test suite
dotnet build MySolution.sln      # target a specific solution
```

Compiled output lands in `bin/Debug/net8.0/` (or `bin/Release/...`).

### Option B — VS Code

1. Install the **C# Dev Kit** extension (Extensions panel, `Ctrl+Shift+X`).
2. Select **File → Open Folder** and select the cloned directory or the downloaded folder.
   Open the folder, not an individual file — the extension needs the project context.
3. This might happen automatically: Accept the prompt to add build and debug assets. This creates `.vscode/tasks.json`.
4. Build with `Ctrl+Shift+B`, or press `F5` to build and start debugging.

The integrated terminal (`` Ctrl+` ``) also works — the Option A commands run there
unchanged. VS Code is an editor, not a build system, so it's calling `dotnet build`
either way.

### Option C — Visual Studio

1. Select **File → Open → Project/Solution**, then pick the `.sln` file.
   *Shortcut:* the start screen has **Clone a repository** — paste the GitHub URL
   and it handles Step 1 for you.
2. Wait for NuGet packages to restore automatically (watch the status bar).
3. Build with `Ctrl+Shift+B`, or **Build → Build Solution**.
4. Press `F5` to run with the debugger, `Ctrl+F5` to run without it.

If the project won't load, open **Visual Studio Installer** and confirm the
**.NET desktop development** or **ASP.NET and web development** workload is installed.

### Quick reference

```bash
git clone <url>
cd <repo>
dotnet build
dotnet run
```

## Run the validator

1. Create the folder **mathml_validator** and place it on **C**: **C:/mathml_validator**.
2. Make a folder named **input**: **C:/mathml_validator/input**. Place the epub you want to validate in this folder.
3. Run the program **Math_ML_Validator.exe** in the folder **Math_ML_Validator/bin/Debug/net9.0/**.
4. In the folder **C:/mathml_validator**, there should now be a folder named **report**. In this folder you find a report in both html and json.
