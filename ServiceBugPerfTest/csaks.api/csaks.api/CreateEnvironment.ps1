$Subscription="evhc-pay-as-you-go";
$KubernetesResourceGroupName="EVHC-Kubernetes-Test-CL";
#Registry name should not have _ or -
$ContainerRegistryName="AZACRCS";
$AppInsightsName="EVHCAppInsightCS";
$KubernetesClusterName="JPSAKSCS";
$KeyVaultServiceName="EVHCKeyCS";
$ServiceLocation="southeastasia";
$ServiceTag="Service=Kubernetes";
$ForceDelete=$true;
#$Username=Read-Host "User name";
#$Password=Read-Host "Password" -AsSecureString;
#$AzPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password));

#az login -u $Username -p $AzPassword
az account set --subscription $Subscription

function Create-ResourceGroup ($groupName,$location,$tags,$deleteIfExists=$false)
 {
		if((az group exists --name $groupName).ToLower() -eq "false")
		{
				Write-Host Resource group $groupName not exists, creating...;
				az group create --name $groupName --location $location --tags $tags;
				Write-Host Resource group $groupName created.;
		}
		else
		{
				Write-Host Resource group $groupName exists.;
				if($deleteIfExists)
				{
						Write-Host Deleting resource group $groupName...;
						az group delete --name $groupName --yes;
						Write-Host Resource group $groupName deleted.;
						Write-Host Creating resource group $groupName...;
						az group create --name $groupName --location $location --tags $tags;
						Write-Host Resource group $groupName created.;
				}
		}
}

function Create-AppcationInsights($appInsightsName,$groupName,$location,$deleteIfExists=$false)
{
		$existance = [string](az resource list --resource-group $groupName --resource-type "Microsoft.Insights/components")|convertfrom-json|where-object {$_.name -eq $appInsightsName};
		if($null -eq $existance) 
		{
				Write-Host Creating AppcationInsights...
				az resource create --resource-group $groupName --resource-type "Microsoft.Insights/components" --name $appInsightsName --location $location --properties '{\"Application_Type\":\"web\"}'
				Write-Host AppcationInsights $appInsightsName created.
		}
		else
		{
				if($deleteIfExists)
				{
						Write-Host The AppcationInsights $appInsightsName exists.
						Write-Host Deleting the AppcationInsights $appInsightsName...
						az resource delete --resource-group $groupName --resource-type "Microsoft.Insights/components" --name $appInsightsName
						Write-Host The AppcationInsights $appInsightsName deleted.
						az resource create --resource-group $groupName --resource-type "Microsoft.Insights/components" --name $appInsightsName --location $location --properties '{\"Application_Type\":\"web\"}'
						Write-Host AppcationInsights $appInsightsName created.;
				}
				else
				{
						Write-Host The AppcationInsights $appInsightsName exists, will not be re-created.
						return; 
				}
		}
}

function Create-ContainerRegistry ($registryName,$groupName,$deleteIfExists=$false){
		$existance = [string](az acr list)|convertfrom-json|where-object {$_.name -eq $registryName};
		if($null -eq $existance) 
		{
				$nameAvailable = ([string](az acr check-name --name $ContainerRegistryName)|ConvertFrom-Json).nameAvailable;
				if($nameAvailable) 
				{
						Write-Host Creating Container Registry $registryName...;
						az acr create --name $registryName --resource-group $groupName --sku basic
						Write-Host Container Registry $registryName created.;
				}
				else 
				{
						Write-Host The Container Registry name $registryName is not available.;
						Exit;
				}
		}
		else 
		{
				if($deleteIfExists)
				{
						Write-Host The Container Registry $registryName exists.
						Write-Host Deleting the Container Registry $registryName...
						az acr delete --name $registryName
						Write-Host The Container Registry $registryName deleted.
						az acr create --name $registryName --resource-group $groupName --sku basic
						Write-Host Container Registry $registryName created.;
				}
				else 
				{
						Write-Host The Container Registry $registryName exists, will not be re-created.
						return; 
				}

				az configure --defaults acr=$ContainerRegistryName;
		}
}

function Create-KubernetesCluster($kubernetesName,$groupName,$tags,$deleteIfExists=$false)
{
		$existance = [string](az aks list --resource-group $groupName)|convertfrom-json|where-object {$_.name -eq $kubernetesName};
		if($null -eq $existance) 
		{
				Write-Host Creating Kubernetes Cluster...
				# az aks update --resource-group $groupName --name $kubernetesName --disable-cluster-autoscaler
				# az aks create --resource-group $groupName --name $kubernetesName --node-count 1 --enable-vmss --enable-cluster-autoscaler --min-count 1 --max-count 5 --tags $tags
				az aks create --resource-group $groupName --name $kubernetesName --enable-rbac --enable-addons http_application_routing --tags $tags
				# az aks create --resource-group $groupName --name $kubernetesName --tags $tags
				Write-Host Kubernetes Cluster $kubernetesName created.
		}
		else
		{
				if($deleteIfExists)
				{
						Write-Host The Kubernetes Cluster $kubernetesName exists.
						Write-Host Deleting the Kubernetes Cluster $kubernetesName...
						az aks delete --name $kubernetesName --resource-group $groupName --yes
						Write-Host The Kubernetes Cluster $kubernetesName deleted.
						az aks create --resource-group $groupName --name $kubernetesName --tags $tags
						Write-Host Kubernetes Cluster $kubernetesName created.;
				}
				else
				{
						Write-Host The Kubernetes Cluster $kubernetesName exists, will not be re-created.
						return; 
				}
		}

		# Switch to new cluster
		sleep -seconds 30
		kubectl config use-context $kubernetesName;
		# Install Kubernetes-KeyVault-FlexVolume
		kubectl create -f https://raw.githubusercontent.com/Azure/kubernetes-keyvault-flexvol/master/deployment/kv-flexvol-installer.yaml
}

function Create-Keyvault($keyvaltName,$groupName,$tags,$deleteIfExists=$false)
{
		$existance = [string](az keyvault list --resource-group $groupName)|convertfrom-json|where-object {$_.name -eq $keyvaltName};
		if($null -eq $existance) 
		{
				Write-Host Creating Key Vault...
				az keyvault create --resource-group $groupName --name $keyvaltName --tags $tags
				Write-Host Key Vault Cluster $keyvaltName created.
		}
		else
		{
				if($deleteIfExists)
				{
						Write-Host The Key Vault $keyvaltName exists.
						Write-Host Deleting the Key Vault Cluster $keyvaltName...
						az keyvault delete --name $keyvaltName --resource-group $groupName --yes
						Write-Host The Key Vault $keyvaltName deleted.
						az keyvault create --resource-group $groupName --name $keyvaltName --tags $tags
						Write-Host Key Vault $keyvaltName created.;
				}
				else
				{
						Write-Host The Kubernetes Cluster $keyvaltName exists, will not be re-created.
						return; 
				}
		}
}

function Set-Credentials($groupName,$registryName,$kubernetesName,$deleteIfExists=$false)
{
		az acr login --name $registryName;
		$admin=$registryName+"Admin";
		$adminId = ([string](az ad sp list --display-name $admin)|convertfrom-json).objectId;
		if($adminId -gt 0)
		{
				if($deleteIfExists)
				{
						Write-Host The user $admin exists, deleting...
						az ad sp delete --id $adminId

						Write-Host The user $admin is deleted.
				}
				else 
				{
						Write-Host The user $admin exists, will not be re-created.
						return;
				}
		}

		az aks get-credentials --resource-group $groupName --name $kubernetesName;
		$clientId=az aks show --resource-group $groupName --name $kubernetesName --query "servicePrincipalProfile.clientId" --output tsv
		$containerId=az acr show --resource-group $groupName --name $registryName --query "id" --output tsv
		az role assignment delete --role Reader --scope $containerId
		az role assignment create --assignee $clientId --role Reader --scope $containerId
		$password=az ad sp create-for-rbac --name $admin --role Reader --scopes $containerId --query password --output tsv
		$appId=az ad sp show --id http://$admin --query appId --output tsv
		kubectl delete secret acrauth
		kubectl create secret docker-registry acrauth --docker-server $registryName".azurecr.io" --docker-username $appId --docker-password $password --docker-email "john@jpsresources.com"
}

function Set-KeyVaultAccess($groupName,$keyvaltName,$deleteIfExists=$false)
{
		$admin=$keyvaltName+"Admin";
		$adminId = ([string](az ad sp list --display-name $admin)|convertfrom-json).objectId;
		if($adminId -gt 0)
		{
				if($deleteIfExists)
				{
						Write-Host The user $admin exists, deleting...
						az ad sp delete --id $adminId
						Write-Host The user $admin is deleted.
				}
				else 
				{
						Write-Host The user $admin exists, will not be re-created.
						return;
				}
		}

		$keyvaultId=az keyvault show --resource-group $groupName --name $keyvaltName --query "id" --output tsv
		az role assignment delete --role Reader --scope $keyvaultId
		$password=az ad sp create-for-rbac --name $admin --role Reader --scopes $keyvaultId --query password --output tsv
		$appId=az ad sp show --id http://$admin --query appId --output tsv
		az keyvault secret set --vault-name $keyvaltName --name testsecret --value test
		az keyvault set-policy -g $groupName -n $keyvaltName --secret-permissions get --spn http://$admin
		kubectl delete secret kvauth
		kubectl create secret generic kvauth --from-literal clientid=$appId --from-literal clientsecret=$password --type=azure/kv
}

Remove-Item $env:USERPROFILE\.kube\config
Write-Host Start to create Azure environment for Kubernetes service...
Create-ResourceGroup -groupName $KubernetesResourceGroupName -location $ServiceLocation -tags $ServiceTag -deleteIfExists $ForceDelete;
Create-AppcationInsights -appInsightsName $AppInsightsName -groupName $KubernetesResourceGroupName -location $ServiceLocation $deleteIfExists=$ForceDelete;
Create-ContainerRegistry -registryName $ContainerRegistryName -groupName $KubernetesResourceGroupName -deleteIfExists $ForceDelete;
Create-KubernetesCluster -kubernetesName $KubernetesClusterName -groupName $KubernetesResourceGroupName -tags $ServiceTag -deleteIfExists $ForceDelete;
Create-Keyvault -keyvaltName $KeyVaultServiceName -groupName $KubernetesResourceGroupName -tags $ServiceTag -$deleteIfExists $ForceDelete;
Set-Credentials -groupName $KubernetesResourceGroupName -registryName $ContainerRegistryName -kubernetesName $KubernetesClusterName -deleteIfExists $ForceDelete;
Set-KeyVaultAccess -keyvaltName $KeyVaultServiceName -groupName $KubernetesResourceGroupName -$deleteIfExists $ForceDelete;
kubectl create clusterrolebinding kubernetes-dashboard -n kube-system --clusterrole=cluster-admin --serviceaccount=kube-system:kubernetes-dashboard
Write-Host Done.
Write-Host Make sure to modify the docker deployment file with the value $ContainerRegistryName".azurecr.io" for image.