using TechNest.KeycloakIac;

// Точка входу Pulumi: pulumi up → TechNestStack.Create() створює realm, ресурс-сервери, ролі, політики, клієнтів і користувачів
return await Pulumi.Deployment.RunAsync(TechNestStack.Create);
