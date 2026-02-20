# Azure CLI developer credentials proxy for Docker

[![Docker Hub](https://img.shields.io/docker/v/workleap/azure-cli-credentials-proxy?logo=docker)](https://hub.docker.com/r/workleap/azure-cli-credentials-proxy)

This simple containerized application acts as a proxy, **allowing other containerized applications to access Azure developer credentials without installing Azure CLI on each individual container**. It is designed for use in local development environments only.


## Getting started

Add `workleap/azure-cli-credentials-proxy:latest` to your `docker-compose.yml` and mount your Linux or WSL `~/.azure/` directory:

```yaml
version: "3"

services:
  azclicredsproxy:
    image: workleap/azure-cli-credentials-proxy:latest
    ports:
      - "8080:8080"
    volumes:
      - "\\\\wsl$\\<DISTRONAME>\\home\\<USERNAME>\\.azure\\:/app/.azure/" # On Windows with WSL
      - "/home/<USERNAME>/.azure:/app/.azure/" # On Linux
```

Finally, add two environment variables to your containerized applications that use `DefaultAzureCredential` or `ManagedIdentityCredential`:

```yaml
version: "3"

services:
  # azclicredsproxy: [...]

  myservice:
    build: .
    depends_on:
      - azclicredsproxy
    environment:
      - "IDENTITY_ENDPOINT=http://azclicredsproxy:8080/token"
      - "IMDS_ENDPOINT=dummy_required_value"
      # Specify MSI_ENDPOINT below if using "az login --identity" in your service.
      - "MSI_ENDPOINT=http://azclicredsproxy:8080/token"
```


## Motivation

When developers run services on their operating system, they use their personal *Azure identity* (`username@company.com`) to access protected Azure resources, thanks to [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/). The `az login` command caches Azure personal credentials in a local `~/.azure/` directory, which is then used by `DefaultAzureCredential` - specifically `AzureCliCredential`, a part of the former.

When these services run in Azure cloud (App Service, AKS, etc.), protected Azure resources are typically accessed using `ManagedIdentityCredential`, which uses a service principal-based Azure identity authentication mechanism also included in `DefaultAzureCredential`.

However, **when developers attempt to run these same services in Docker locally**, the Docker images do not include Azure CLI. These images also lack access to a service principal. While Dockerfiles can be modified to install Azure CLI, and containers can mount the local `~/.azure/` directory, there are several disadvantages:

* Azure CLI is not suitable for production as an authentication mechanism
* Azure CLI adds a significant 1GB to the Docker image

<img src="https://user-images.githubusercontent.com/14242083/224446793-33930f7f-03b6-4447-8c80-b3b241caba64.png" width="800" />

Despite these issues, developers often use their personal Azure identity in local Docker containers. A [GitHub issue](https://github.com/Azure/azure-sdk-for-net/issues/19167) created in March 2021 remains open.


## Solution

Instead of installing Azure CLI in each service, we can run another container - a proxy, which is the only one that contains Azure CLI and a mount on `~/.azure/`. This container exposes a single endpoint that returns the Azure developer credentials retrieved with Azure CLI.

Then, we must add two environment variables to each service:

* `IDENTITY_ENDPOINT`: the URL of the proxy endpoint (e.g., `http://azclicredsproxy:8080/token`)
* `IMDS_ENDPOINT`: an arbitrary but mandatory value (e.g., `random-placeholder`)

With these two environment variables, any service that uses `DefaultAzureCredential` or `ManagedIdentityCredential` will now call the proxy when Azure credentials are needed. This is because one of `ManagedIdentityCredential`'s [source implementations](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.6.0/sdk/identity/Azure.Identity/src/AzureArcManagedIdentitySource.cs) explicitly looks for both of these environment variables if they are specified.

> [!NOTE]
> If you are using `az cli` in your service and your service wants to do `az login --identity` then:  
> For `az cli` version < 2.74: specify `MSI_ENDPOINT`: the URL of the proxy endpoint (e.g., `http://azclicredsproxy:8080/token`) environment variable instead. `IDENTITY_ENDPOINT` and `IMDS_ENDPOINT` are not required for `az login --identity`.  
> For `az cli` v2.74 and above: Specify `IDENTITY_ENDPOINT`: the URL of the proxy endpoint (e.g., `http://azclicredsproxy:8080/token`) environment variable instead along with `IDENTITY_HEADER`: an arbitrary but mandatory value (e.g., "random-placeholder").

With this proxy, Dockerfiles can remain untouched and production-ready. The proxy can easily be added to an existing `docker-compose.yml`, and the environment variables are also easy to add. Now, the containerized environment looks like this:

<img src="https://user-images.githubusercontent.com/14242083/224446855-35880df8-1ccd-42df-b226-5afa7b93caa6.png" width="800" />


## Notes

Keep in mind that you cannot mount a Windows-based `~/.azure/` credentials directory to a Linux container. On Windows, the credentials file cache is a binary file encrypted with [DPAPI](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection). On Linux, DPAPI is not supported and the file is not encrypted.

The solution is to use `az login` on your WSL distribution and mount `\\wsl$\Ubuntu\home\<WSLUSERNAME>\.azure\` instead of `%USERPROFILE%\.azure\`.


## License

Copyright © 2023, [Workleap Inc.](https://workleap.com/). This code is licensed under the Apache License, Version 2.0. You may obtain a copy of this license at https://github.com/workleap/gsoft-license/blob/master/LICENSE.
