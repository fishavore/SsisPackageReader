# SSIS Package Reader

## Summary

Document SSIS package content.

## Revision history

- 2016-02-15 Updated ReadMe.md and re-uploaded files.
- 2016-01-12 Created this GitHub repo and uploaded files.

## Description

SSIS Package Reader provides free-form entry of a search term and returns all entries in the supported tasks that contain that text.  
  
Supported tasks are:

- Data Flow Task
  - Destination
  - Source
- Execute SQL Task

**Note:** This project was completed in a private Bitbucket repository.

## References

- Environment
  - Sample dtsx package files were created using SQL Server 2008.
  - SSIS Package Reader was created using Visual Studio 2012 with .NET Framework 4.5, Client Tools SDK, SQL Server Data Tools – Business Intelligence for Visual Studio 2012, and Microsoft Connectors v2.0 for Oracle and Teradata.
- Programming
  - Developer's Guide (Integration Services): https://msdn.microsoft.com/en-us/library/ms136025.aspx
  - Required assemblies: Microsoft.SQLServer.ManagedDTS.dll (Microsoft.SqlServer.Dts.Runtime Namespace) and Microsoft.SqlServer.SQLTask.dll (Microsoft.SqlServer.Dts.Tasks.ExecuteSQLTask Namespace)

**Note:** Other references are given in the source code.
