# Crossbill.LicenseNoticeAggregator utility for aggregating third-party license notices in a single file

Crossbill.LicenseNoticeAggregator is the utility application which generates a single file with all license notices from the third-party NuGet packages referenced by a .NET project file.

The utility processes the .csproj file. It automatically takes the referenced NuGet package file from a local NuGet packages cache folder or attempts to download a package file. Once the file is obtained the utility tries to get the third-party licensing information from the NuGet package file. If the utility fails to find the license information, the notification is written to the log file `licenses.log`. All failed license notices can be obtained manually and put in the `ManualCheck/` directory. So, next run, such licensing information will be included in the result file.

The result is a single text file with all third party license notices listed in it. The file can later be included together with the release version of the application.

## Release version

The compiled version of the utility can be obtained from the release section of this repository.

## Usage

### Call from CLI

The utility can be run from the command line interface as follows:
```
c:\Crossbill.LicenseNoticeAggregator\Crossbill.LicenseNoticeAggregator.exe /project "d:/Projects/Crossbill.Plugins/Crossbill.Central.Agent.Plugins.Cloudflare/Crossbill.Central.Agent.Plugins.Cloudflare.csproj" /output "d:/Projects/Crossbill.Plugins/Crossbill.Central.Agent.Plugins.Cloudflare/plugins/Crossbill.Central.Agent.Plugins.Cloudflare/third-party-notices.txt" /exclude "c:\Crossbill.LicenseNoticeAggregator\Crossbill.Central.Agent" /extra "c:\Crossbill.LicenseNoticeAggregator\extra"
```

### Call from a Visual Studio project file
The utility can be run during the project compilation using the following configuration:
```
<Target Name="PreBuild" BeforeTargets="PreBuildEvent" Condition="'$(Configuration)' == 'Release'">
	<Exec Command="c:\Crossbill.LicenseNoticeAggregator\Crossbill.LicenseNoticeAggregator.exe /project &quot;$(ProjectPath)&quot; /output &quot;$(ProjectDir)plugins\$(ProjectName)\third-party-notices.txt&quot; /exclude &quot;c:\Crossbill.LicenseNoticeAggregator\Crossbill.Central.Agent&quot;" />
</Target>
```

## Supported parameters
* /project - the full path to .csproj file to gather the third-party references;
* /output - the full path to the produced text file with the license notices;
* /exclude - optionally, the full path to the directory which contains already processed third-party notices. The parameter is used if we package the submodule as a part of a bigger application, which already includes some third-party licensing notices.
* /extra - optionally, the full path to the directory which contains the third-party notices that should be included in the result file anyway.

## Common directories
The following directories under the application home directory will be processed automatically:
* ManualCheck/ - can contain license notices produced manually. The directory used for the NuGet packages which fails to be processed automatically. Such packages may miss the license notice or use nonstandard naming for the license file.
* ExtraLicenses/ - used when no /extra parameter is specified for the third-party notices which should be included in the result file anyway.

## Sample result

The sample result of the Crossbill.LicenseNoticeAggregator work can be found in [third-party-notices.txt](third-party-notices.txt) file.

## License

The Crossbill Software License Agreement is located in [LICENSE.txt](LICENSE.txt) file.

The Third Party Code in Crossbill Products notice is located in [third-party-code.txt](third-party-code.txt) file.

The copyright and license texts for the third party code can be found in [third-party-notices.txt](third-party-notices.txt) file.

