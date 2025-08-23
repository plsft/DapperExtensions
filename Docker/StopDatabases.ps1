﻿[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [ValidateSet($true, $false)]
    [boolean]$Shutdown = $false
)

wsl docker container stop docker_mariadb_1
wsl docker container stop docker_oracle-db_1
wsl docker container stop docker_sqlserver_1
wsl docker container stop docker_mariadbadminer_1

wsl -u root -- service docker stop

if ($Shutdown)
{
    wsl --shutdown
}
