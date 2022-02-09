<img src="Banner.png" style="width:50%;max-width:1200px;margin-bottom:4em;"/>

# Avant Garde #
**Avant Garde** is an AXAML previewer for the C# Avalonia Framework.

**NB.** *Currently in "alpha".*

It has the following features:

* Avant Garde is a standalone application, rather than an IDE extension. This means you can use it in conjunction with
any IDE where it may be pinned to stay-on-top in any area of spare desktop space.

* It provides a read-only view of your Avalonia project, watching for file changes. It is currently a read-only
previewer, rather than a designer or editor (it does not modify your project files). While it is also able to browse
image assets and text encoded files, the generation of AXAML previews is the primary use case.

* Previews are updated instantly the moment you save AXAML changes from your IDE.

* It's built-in **Project Explorer** can be conveniently closed to the side so as to minimize the window foot-print
while you focus on a particular form.

* It support mouse interaction with previews, and a range of other features.

Avant Garde requires the installation of the **Avalonia Nuget Package** in order to work because it utilizes the
"preview host" which ships with Avalonia. It supports only .NET projects (not .NET Framework), and Avalonia
AXAML forms. It cannot be used to preview WPF XAML.

Avant Garde is licensed under GPLv3. See license for details.

## Screenshots ##
Shown, below, Avant Garde in the "Fluent Light" theme with a preview of its "About" window. The display of grid
highlighting and grid lines are optional.

[![Avant Garde Screenshot](AvantGarde-screenshot-main.png)](AvantGarde-screenshot-main.png)

Here, Avant Garde floating on top of the Visual Code IDE on Linux. This time, the application is shown in the
Fluent Dark theme and its Project Explorer has been closed.

[![Avant Garde Screenshot](AvantGarde-screenshot-ide.png)](AvantGarde-screenshot-ide.png)

## Using Avant Garde ##

Simply open a *.sln or *.csproj as you would in any IDE.

By the default, only "*.axaml;*.xaml" and image files are shown in the Project Explorer as these are the primary
files you will want to see in Avant Garde (the rest of your project will be hidden but see below).

Avant Garde maintains its own "settings" pertaining to each solution you open with the application. Normally, you will
not need to change these. If you do make changes, these changes will be saved between launches so that you need only
make them once.

**Important.** For example, in order to generate previews, Avant Garde must find your application assembly which
must first be built. Normally, if your assembly is in the usual place, i.e. under "project/bin/Debug...", Avant Garde
will find it. If, however, if you are using variables or a "Directory.Build.props" file to set your output location,
you will need to specify this location at the project level (below).

### Solution Settings ###
Solution settings pertain on a per solution basis and are saved between launches of Avant Garde. They are specific
to Avant Garde and changes do not modify any project file on disk.

Click "Edit -> Solution", or the "cog icon" in the toolbar.

[![Avant Garde Screenshot](AvantGarde-screenshot-solution.png)](AvantGarde-screenshot-solution.png)

For example, setting "Include File Pattern" to "*" will cause all project files to be shown in the Explorer.

### Project Settings ###
Project settings pertain to each project within a solution.

Click "Edit -> Project", or the "cog icon" beside the project in the Explorer.

[![Avant Garde Screenshot](AvantGarde-screenshot-project.png)](AvantGarde-screenshot-project.png)

As described, if your target assembly output cannot be located on disk, this is the place to specify it, as shown above.

Moreover, if your project is a class library, it will be necessary to specify a relavent application project
before previews can be generated.


## Additional Information ##
Avant Garde was created by Andy Thomas at https://kuiper.zone

