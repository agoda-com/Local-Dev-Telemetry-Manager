# DevExTelemetry
[![logs badge](https://gitlab.agodadev.io/groups/full-stack/tooling/-/wikis/uploads/fe3da596b693454dc9d5765cafc146d7/logs.svg)](https://grafana.agodadev.io/explore?orgId=1&left=%7B%22datasource%22:%22gP4MlSq7k%22,%22queries%22:%5B%7B%22refId%22:%22A%22,%22datasource%22:%7B%22type%22:%22loki%22,%22uid%22:%22gP4MlSq7k%22%7D,%22editorMode%22:%22code%22,%22expr%22:%22%7BapplicationName%3D%5C%22change-this-to-project-component-name%5C%22%7D%22,%22queryType%22:%22range%22%7D%5D,%22range%22:%7B%22from%22:%22now-1h%22,%22to%22:%22now%22%7D%7D)
[![app insights badge](https://gitlab.agodadev.io/groups/full-stack/tooling/-/wikis/uploads/f2366bdd59bda789ddd7fbe6eb0facfd/app-insights.svg)](https://grafana.agoda.local/d/VIcCaNjik/application-insights?refresh=1m&orgId=1&var-App=change-this-to-project-component-name&var-DC=All)

This project is generated from [GitLab Dotnet Template (tag: v7)](https://gitlab.agodadev.io/full-stack/templates/gitlab-dotnet-templates/-/tree/v7)

## Please complete deployment and CI setup as stated [in step 3 and 4 of the template's README](https://gitlab.agodadev.io/full-stack/templates/gitlab-dotnet-templates/-/tree/v7#3-configure-deployment), then you can remove this line from README

## Deployment

### Production
DNS (ingress) names and Consul service registration are configured automatically by Privatecloud.
Resource described in table below will be accessible when this project is deployed.

| DC | Domain | Consul |
| --- | --- | --- |
| HK | https://change-this-to-project-component-name-production.privatecloud.hk.agoda.is/ | [Consul HK](https://consul.agoda.local:8501/ui/hk/services/change-this-to-project-component-name) |
| SG | https://change-this-to-project-component-name-production.privatecloud.sg.agoda.is/ | [Consul SG](https://consul.agoda.local:8501/ui/sg/services/change-this-to-project-component-name) |
| AS | https://change-this-to-project-component-name-production.privatecloud.as.agoda.is/ | [Consul AS](https://consul.agoda.local:8501/ui/as/services/change-this-to-project-component-name) |
| AM | https://change-this-to-project-component-name-production.privatecloud.am.agoda.is/ | [Consul AM](https://consul.agoda.local:8501/ui/am/services/change-this-to-project-component-name) |

This project may also use different DNS configuration by following this [guide](https://confluence.agodadev.io/display/PRIVATECLOUDDOC/Fleet+DNS#FleetDNS-Option3:Useagoda.localDNSasdefaultDNS). But most of the cases, you don't need to do anything.

See [deployment.prod.yml](deployment.prod.yml)

### QA
https://change-this-to-project-component-name.qa.agoda.is/

See [deployment.qa.yml](deployment.qa.yml)


## Monitoring
### Measurements
- [Application Insights (serverside)](https://grafana.agoda.local/d/VIcCaNjik/application-insights?refresh=1m&orgId=1&var-App=change-this-to-project-component-name&var-DC=All)
- [Private Cloud Traffic monitoring](https://grafana.agodadev.io/d/eipPfbaGz/private-cloud-traffic-monitoring?orgId=1&refresh=1m&var-service=production.change-this-to-project-component-name.svc.cluster.local)
  - More private cloud dashboards. See [Monitoring in Private Cloud](https://confluence.agodadev.io/display/PRIVATECLOUDDOC/Monitoring+in+Private+Cloud)
### Logs
centrallized-logging on **Log Kestrel**: query [`applicationName: change-this-to-project-component-name`](https://grafana.agodadev.io/explore?orgId=1&left=%7B%22datasource%22:%22gP4MlSq7k%22,%22queries%22:%5B%7B%22refId%22:%22A%22,%22datasource%22:%7B%22type%22:%22loki%22,%22uid%22:%22gP4MlSq7k%22%7D,%22editorMode%22:%22code%22,%22expr%22:%22%7BapplicationName%3D%5C%22change-this-to-project-component-name%5C%22%7D%22,%22queryType%22:%22range%22%7D%5D,%22range%22:%7B%22from%22:%22now-1h%22,%22to%22:%22now%22%7D%7D)

## Develop
### Setup
#### SDK
Make sure you have these followings installed
1. Install [Visual Studio 2022](https://visualstudio.microsoft.com/vs/#download) with the __ASP.NET and web development workload__.
1. nodejs version 16+ (you could also use [nvm](https://github.com/nvm-sh/nvm) (for macOS/Linux) or [nvm-windows](https://github.com/coreybutler/nvm-windows) to manage node versions)

#### IDE
Visual Studio 2022 or Rider 2022.3+

### Launch
In .net IDE, open solution file _([`Agoda.DevExTelemetry.sln`](src/Agoda.DevExTelemetry.sln))_ 
and there is a launch configuration already recognized by IDE that you can press start button. 
(see: [`launchSettings.json`](src/Agoda.DevExTelemetry.WebApi/Properties/launchSettings.json))

### Seeing logs on local
You could see logs in output in your IDE of course. 
But this project equipped with [Seq](https://datalust.co/seq) log sinks out-of-the-box.
You will only need to install Seq on your local and you see log at `localhost:5341`
