# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]


### Documentation

- **changelog:** Update generated changelog

- **changelog:** Update generated changelog



## [v1.0.0] - 2026-08-06


### Added

- Add read tracking functionality and read count formatting

- Enhance database migration script with Preflight action and credential validation

- Add story completion status feature

- Add release automation workflow and changelog generation


### Changed

- Remove unused Aspire.Cloudflared namespace from AppHost.cs


### Fixed

- Increase timeout for copilot setup steps and clean up NuGet package source mapping

- Update package name from Shirubasoft.Aspire.Cloudflared to Shirubasoft.Aspire.CloudflareTunnels

- Remove unused package references and improve test assertions


### Other

- Update dependency MongoDB.EntityFrameworkCore to 10.0.2

- Add nonce support to Disqus integration for enhanced security

- Update dotnet monorepo

- Update dependency Markdig to 1.3.2

- Update dependency SharpCompress to 0.49.1

- Update mongo-csharp-driver monorepo to 3.9.0

- Update opentelemetry-dotnet monorepo to 1.16.0

- Update scalar monorepo to 2.16.4

- Update actions/checkout action to v7

- Update testcontainers-dotnet monorepo to 4.12.0

- Update wolverine monorepo to 6.13.1

- Update dependency StreamJsonRpc to 2.25.29

- Update aspire monorepo to 13.4.6

- Update scalar monorepo

- Update wolverine monorepo to 6.14.0

- Update opentelemetry-dotnet-contrib monorepo to 1.16.0

- Update actions/setup-dotnet digest to 26b0ec1

- Update actions/attest action to v4.1.1

- Update dependency Scalar.AspNetCore to 2.16.6

- Update wolverine monorepo to 6.16.0

- Update dependency Scalar.AspNetCore to 2.16.7

- Update github/codeql-action digest to 54f647b

- Update dependency Scalar.AspNetCore to 2.16.10

- Update dotnet monorepo to 5.6.0

- Update testcontainers-dotnet monorepo to 4.13.0

- Update mongo-csharp-driver monorepo to 3.10.0

- Update github/codeql-action digest to 99df26d

- Update dependency Scalar.AspNetCore to 2.16.11

- Update dependency Npgsql.EntityFrameworkCore.PostgreSQL to 10.0.3

- Update dependency Microsoft.Testing.Platform to 2.3.1

- Update dependency Microsoft.EntityFrameworkCore.DynamicLinq to 10.7.3

- Update dependency OpenTelemetry.Instrumentation.Runtime to 1.16.0

- Update dependency Microsoft.Testing.Platform to 2.3.2

- Update wolverine monorepo to 6.19.0

- Update dependency SharpCompress to 0.50.0

- Update dotnet monorepo

- Update github/codeql-action digest to 7188fc3

- Update dependency Scalar.AspNetCore to 2.16.15

- Update actions/attest action to v4.2.0

- Update opentelemetry-dotnet monorepo to 1.17.0

- Update actions/checkout digest to 3d3c42e

- Update dependency Scalar.AspNetCore to 2.16.16

- Update github/codeql-action digest to e4fba86

- Update dependency Mongo2Go to 4.2.0

- Update opentelemetry-dotnet-contrib monorepo to 1.17.0

- Update wolverine monorepo to 6.22.0

- Update dependency GitHubActionsTestLogger to 3.0.5

- Update dependency SharpCompress to 0.50.1

- Update wolverine monorepo to 6.24.0

- Update actions/attest action to v4.2.1

- Update dependency Microsoft.Testing.Platform to 2.3.3

- Update github/codeql-action digest to f205ea1

- Update dependency Scalar.AspNetCore to 2.16.17

- Update dependency SharpCompress to 0.50.3

- Implement CSP Reporting System with Storage and Parsing Logic

- Added BrowserReportParser for deserializing CSP reports from legacy and modern formats.
- Created BrowserReports to define various report types including CSP violations, crashes, and deprecations.
- Developed CspReportStorageService to handle storage of CSP reports in the database, including handling duplicates and generating fingerprints.
- Introduced CspReportingServiceCollectionExtensions for dependency injection of the storage service.
- Updated Routes.razor to simplify routing logic by removing unnecessary CascadingValue.
- Enhanced theme.js to manage Blazor error UI visibility.
- Added unit tests for CspReportStorageService to ensure correct functionality of report deserialization and storage.

- Update project configuration and dependencies for improved compatibility and performance

- Refactor deploy-infra script for improved deployment process and add graceful update functionality

- Remove vulnerable package references for improved security

- Add feature workflow documentation and architecture references

- Enhance project setup and documentation

- Updated GitHub Actions workflow to include .NET SDK installation from global.json and added environment variables for .NET CLI.
- Revised code style and conventions documentation to reflect current practices and preferences.
- Improved suggested commands documentation for clarity on local tool usage and command execution order.
- Expanded tech stack documentation to include detailed descriptions of technologies and their roles in the project.
- Clarified post-task completion steps in the documentation to ensure proper build and test procedures.
- Added mandatory repository context guidelines to AGENTS.md for better code change practices.
- Updated architectural documentation to emphasize the use of .NET 10 and its benefits for the project.
- Removed resolved limitations regarding Docker Swarm deployment labels and clarified production configuration details.
- Enhanced README with clearer instructions for setting up the project and running the application.
- Modified agent bootstrap scripts to support forcing local source generator package publication and improved error handling.

- Update dependency SonarAnalyzer.CSharp to 10.31.0.145097

- Apply targeted analyzer fixes for release build

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Update dependency Shirubasoft.Aspire.Cloudflared to 1.1.1

- Update actions/setup-dotnet action to v6

- Update dependency NSubstitute to v6

- Update dependency Mongo2Go to v5



## [v1.0.0-alpha6] - 2026-06-19


### Other

- Update aspire monorepo to 13.3.5

- Update dependency Microsoft.Testing.Platform to 2.2.3

- Update github/codeql-action digest to 7211b7c

- Update dependency Scalar.AspNetCore to 2.14.14

- Update dependency SharpCompress to 0.48.1

- Update dependency WolverineFx to 5.39.3

- Update dependency Microsoft.OpenApi to 2.7.5

- Update About page title to include heart emoji for branding consistency

- Refactor OpenTelemetry configuration to streamline exporter addition logic

- Fix OpenApi document generation condition for CI builds

- Update dependency SonarAnalyzer.CSharp to 10.27.0.140913

- Enhance service location policy and code generation for FictionDbContext

- Update WolverineFx to version 6.2.2 and add WolverineFx.RuntimeCompilation package

- Update actions/setup-dotnet digest to 9a946fd

- Update dependency Npgsql to 10.0.3

- Update dependency Npgsql.DependencyInjection to 10.0.3

- Update dependency Npgsql.EntityFrameworkCore.PostgreSQL to 10.0.2

- Update dependency Npgsql.OpenTelemetry to 10.0.3

- Update github/codeql-action digest to 87557b9

- Update github/codeql-action digest to 8aad20d

- Update actions/checkout digest to df4cb1c

- Refactor source generator integration and update project configurations

- Added IHFiction.SourceGenerators package with initial version 0.1.0-local.
- Implemented OpenAPI client and endpoint registration generators.
- Updated .gitignore to exclude unnecessary files.
- Removed obsolete IHFiction.ApiClientGenerator project.
- Enhanced StoryReader component to require Meta parameter.
- Updated agent bootstrap scripts for local source generator package management.
- Added release notes for source generators.

- Add local NuGet package and update .gitignore to include package files

- Remove SharpCompress package references and update NuGet.config for package source mapping

- Remove Snappier package references due to known vulnerabilities

- Update package versions and secure OIDC authority URLs in configuration files

- Update Mattraks/delete-workflow-runs digest to 0cf693b

- Add Disqus integration and update dependencies

- Introduced Disqus options and components for comment functionality.
- Updated appsettings.json to include Disqus ShortName.
- Added DisqusThread component to StoryDetail and StoryReader pages.
- Included StreamJsonRpc package version 2.25.28 in Directory.Packages.props.
- Updated Docker image tag in AppHost.cs.

- Enhance source generator packaging with content hash comparison and cleanup

- Add WORKAROUNDS.md for documenting temporary build workarounds and update AGENTS.md to reference it



## [v1.0.0-alpha5] - 2026-05-22


### Added

- Update metadata titles and descriptions to include heart emoji for branding consistency


### Changed

- Remove NavigationManager injection and enhance URL handling across components

- Remove obsolete unit tests for story and tag functionalities


### Other

- Refactor metadata URL formatting duplication

- Potential fix for pull request finding

Co-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>

- Potential fix for pull request finding

Co-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>

- Handle suppressed metadata URL review comments

Agent-Logs-Url: https://github.com/SheepReaper/IHeartFiction/sessions/72ebd05c-2e2a-4640-82e0-4eaa82bfdc04

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Refine URL scheme checks and expand unsafe-scheme tests

Agent-Logs-Url: https://github.com/SheepReaper/IHeartFiction/sessions/72ebd05c-2e2a-4640-82e0-4eaa82bfdc04

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Add low-value test finder skill and valuation rubric; implement heuristic scanner for low-value tests

- Refactor author avatar URL generation

- Implement lightweight story metadata loader and update GetPublishedStoryContent to use it

- Add query filter for device notification deliveries to exclude deleted authors

- Add route values support for pagination links in story chapters

- Optimize query for published stories by removing unnecessary includes and using AsNoTracking for better performance

- Update github/codeql-action digest to 9e0d7b8

- Update dependency coverlet.collector to 10.0.1

- Implement persistent state management for various components and add noindex meta tag for SEO

- Refactor story and author pages; introduce WorkService for handling published works

- Removed AuthorDetailPage and ChapterReader components.
- Added StoryDetailPage to display detailed information about stories.
- Updated StoryReaderPage to work with the new WorkService and handle reading works.
- Introduced WorkService for fetching published work metadata and content.
- Updated DynamicSitemapNodeProvider to include new routes for reading works.
- Enhanced CSS styles for improved layout and readability.
- Added unit tests for GetPublishedWork functionality to ensure correct behavior for various work types.

- Add runtime identifiers for cross-platform support; update page titles for consistency



## [v1.0.0-alpha4] - 2026-05-18


### Added

- Implement structured data and metadata composition guardrails across components



## [v1.0.0-alpha3] - 2026-05-18


### Added

- Implement compact view mode across story components

- Enhance Markdown Editor with dynamic script loading and wait functionality



## [v1.0.0-alpha2] - 2026-05-17


### Other

- Refactor device identifier errors

- Add cloud agent preflight scripts and update workflow documentation

- Refactor sitemap node provider to use constructor parameter directly and improve node retrieval logic

- Add sourcemaps

- Add font loading behavior for Font Awesome 5 and 6

- Enhance accessibility and keyboard navigation for user account menu and mobile menu

- Refactor LinkService to use new syntax for route values and enhance pagination links in ListPublishedStories

- Enhance API responses with link metadata

- Added link generation to various endpoints for improved navigation and resource discovery.
- Updated methods in Account, Stories, and Tags modules to include `LinkService` for creating links in responses.
- Ensured that all relevant endpoints now return linked results, enhancing the API's usability and compliance with HATEOAS principles.

- Enhance LoginDisplay and NavMenu components with action invocation and mobile menu closure

- Fix theme icon display logic in NavMenu component



## [v1.0.0-alpha1] - 2026-05-15


### Added

- Enhance source generator integration by copying analyzers to intermediate output

- Add dry-refactor-pr skill for focused DRY refactoring


### Changed

- Update CodeQL workflow to remove manual build mode for C# and clean up unnecessary steps; add notes to WIP document


### Fixed

- Update attest action to v4.1.0 for build provenance


### Maintenance

- **deps:** Pin actions/attest action to 59d8942

- **deps:** Update dependency scalar.aspnetcore to 2.14.11

- **deps:** Update dependency microsoft.sourcelink.github to v10

- **deps:** Update dependency minver to v7

- **deps:** Update github/codeql-action digest to 68bde55

- **deps:** Update dependency wolverinefx to 5.39.0

- **deps:** Update mongo-csharp-driver monorepo to 3.8.1

- **deps:** Update dotnet monorepo

- **deps:** Update dependency markdig to 1.2.0


### Other

- Remove outdated Playwright CLI documentation and references; update dependencies to version 13.3.0; add SharpCompress package across multiple projects; introduce MCP server configuration.

- Refactor integration test service setup

- Documents preferred logging patterns

Clarifies the default C# logging style so new work follows analyzer-friendly source generation instead of older delegate caching.

Keeps contributor guidance and local ignore rules aligned with the current tooling setup.

- Splits notification handlers by concern

Separates unrelated notification behaviors into focused units so the area is easier to navigate, review, and extend.

Reduces coupling between inbox queries and push registration flows without changing their external behavior.

- Cleans up stale device subscriptions

Removes anonymous push subscriptions when a device no longer follows anything, which prevents orphaned records and avoids sending notifications that no longer have a reason to exist.

Adds regression coverage so the cleanup only happens when the last follow is gone.

- Adds web push fanout support

Extends notification delivery beyond stored inbox rows so browser subscribers can be notified immediately.

Wires VAPID configuration through the application stack, handles expired subscriptions gracefully, and adopts source-generated logging for clearer operational signals.

- Fixes local authority configuration

Applies the configured identity authority in every environment so local and build-time runs do not silently skip it.

Pins the local identity host to a stable address to keep token validation and client setup predictable.

- Improves web client routing flow

Makes navigation and error handling more predictable by adding an explicit not-found route and updating middleware order around redirects, auth, caching, and static assets.

Also prepares the shell for broader resource loading and service worker scope handling.

- Moves push UI to JS modules

Replaces global push helpers with module-based interop so subscription logic is loaded on demand and cleaned up with the component lifecycle.

Consolidates service worker assets into shared content and removes duplicated browser-side push code.

- Merges device and account notifications

Preserves continuity after sign-in by combining anonymous and authenticated follow and inbox data instead of treating them as separate worlds.

Introduces client interfaces and focused tests so the notification service becomes easier to substitute and verify.

- Normalizes path separators

Uses slash-based paths so build configuration and contributor guidance stay consistent across shells and operating systems.

Reduces avoidable command and copy-target issues when working outside Windows.

- Shares web push options

Moves notification settings into a shared location so server and client code rely on the same configuration contract.

Updates imports and option wiring to support reuse, and removes API-specific visibility assumptions that no longer fit a shared model.

- Configures push subscription keys

Replaces the hardcoded browser push key with injected configuration so each deployment can provide the correct value without editing shipped assets.

Keeps the subscription flow aligned between Razor components and the JavaScript helper.



## [v1.0.0-test1] - 2026-05-09


### Added

- Add book metadata update functionality and improve input sanitization

- Enhance ClaimsPrincipalExtensions with additional error handling and update username retrieval logic

- Add ApiBaseAddress configuration to enhance security headers and service integration

- **migrations:** Rename 'work' table to 'works' and update related foreign keys

- Implement StoryEditorService for managing stories, books, and chapters

- Update script references in App.razor and enhance middleware configuration in Program.cs

- Enhance MarkdownEditor with development script caching and logging

- Improve error handling in MarkdownEditor with best-effort logging and update deployment configuration

- Enhance About and StoryEditor pages with maintainer information and loading indicators

- Increase maximum character limits for notes in chapter and story content

- Enhance database configuration with query splitting behavior and improve error handling in StoryEditor

- Update stylesheet links to preload for improved performance and clean up About page styling

- Update Content Security Policy to allow inline scripts for attributes (rocket-loader)

- Integrate Sidio Sitemap for enhanced SEO and add sitemap attributes to relevant pages

- Enhance sitemap integration by adding dynamic sitemap generation and updating sitemap attributes on relevant pages

- Add GitHub workflows for Copilot setup, labeler cache retention, prediction, promotion, and training

- Enhance AuthorWorkItem to include PublishedAt and update related logic

- Implement ordering for chapters and books in stories

- Add SignInAgain page to handle re-authentication with user feedback

- Add BookTitle to chapter content response and update related components

- Add uptime endpoint with support for HEAD and GET methods

- Update favicon assets and add web app manifest for improved branding

- Enhance dynamic sitemap with authors and stories endpoints

- Feat: add Aspire skill documentation and configuration files
chore: update package versions in Directory.Packages.props
fix: correct spelling of "Ko-fi" in About.razor

- Update Sidio.Sitemap.Blazor package to version 2.0.1 and add DynamicSitemapNodeProvider class (not wired up yet, due to https://github.com/marthijn/Sidio.Sitemap.Core/issues/99)

- **theme:** Add URL parameter support for theme selection

- **docs:** Add initial documentation files for project purpose, tech stack, suggested commands, and task completion instructions

- **docs:** Add repository-specific theming guidance for UI checks

- **reading:** Enhance chapter navigation and reader progress management

- **responsive:** Enhance mobile reading experience with adaptive styles

- **dark-theme:** Improve link styling in dark mode for better readability

- **social-metadata:** Implement social preview metadata for improved sharing and SEO

- **auth:** Enhance book access authorization and add integration tests

- **configuration:** Add SiteUrlOptions for configurable base URL and update metadata handling

- **social-metadata:** Enhance social share preview standards and implement validation checks

- **social-metadata:** Update Twitter metadata handling for improved sharing and SEO

- **become-author:** Update author guidelines for clarity and community standards

- Update user profile management and enhance author features

- Enhance authentication and user experience

- Add CoverImageUrl to SocialPreviewMetadata for enhanced story sharing

- Integrate WolverineFx for asynchronous workflows and messaging across services

- Implement push notifications and following system

- Enhance following and notification features with new UI components and styles

- Update build configuration and dependencies for improved CI support and versioning


### Changed

- Update author-related queries to include all works and improve pagination descriptions


### Fixed

- Correct capitalization in API description and summary for consistency

- Update Source parameter to use Get-Location for better path resolution

- Add support for actions language in CodeQL analysis and update build conditions for C#

- Count only published stories in GetAuthorById response

- Update workflow conditions to reflect repository ownership and enable default label

- Remove obsolete connection string from web client service configuration

- Increase maximum message size for content editor to handle large pastes

- Render chapter notes as HTML using Markdown

- **aspire:** Stabilize multi-arch push pipeline and align Swarm publishing with new Aspire APIs

- **aspire:** Configure Docker healthcheck start interval for Fiction API

- Update coverlet.collector version to 8.0.1 and adjust settings for terminal auto-approval

- Update Markdig version to 1.1.2


### Maintenance

- **deps:** Remove deprecated manager from enabledManagers and adjust packageRules for FluentAssertions

- **deps:** Update dependency mongodb.driver to 3.4.3

- **deps:** Update dotnet monorepo

- **deps:** Update dependency microsoft.openapi to 2.3.0

- **deps:** Update dependency mongodb.entityframeworkcore to 9.0.1

- **repo:** Add abandonment threshold for GitHubActionsTestLogger package in Renovate configuration

- **deps:** Update aspire monorepo to 9.4.2

- **deps:** Update testcontainers-dotnet monorepo to 4.7.0

- **deps:** Update github/codeql-action digest to 2d92b76

- **deps:** Update actions/setup-dotnet action to v5

- **deps:** Update dependency keycloak.authservices.authorization to 2.7.0

- **deps:** Update github/codeql-action digest to f1f6e5f

- **deps:** Update dependency microsoft.openapi to 2.3.1

- **deps:** Update github/codeql-action digest to 192325c

- **deps:** Update dependency scalar.aspnetcore to 2.8.1

- **deps:** Update dotnet monorepo

- **deps:** Update dependency mongodb.driver to 3.5.0

- **deps:** Update dependency scalar.aspnetcore to 2.8.3

- **deps:** Update dependency markdig to 0.42.0

- **deps:** Update package versions to 9.5.0 for Aspire.Hosting and related dependencies

- **deps:** Update dependency microsoft.entityframeworkcore.dynamiclinq to 9.6.8

- **deps:** Update dependency scalar.aspnetcore to 2.8.8

- **deps:** Update xunit-dotnet monorepo

- **deps:** Update dependency nswag.apidescription.client to 14.6.1

- **deps:** Update dependency microsoft.openapi to 2.3.2

- **deps:** Update github/codeql-action digest to 64d10c1

- **deps:** Update dependency newtonsoft.json to 13.0.4

- Update copilot instructions, refine limitations, and add deployment scripts

- **deps:** Update aspire monorepo to 9.5.1

- **deps:** Update dependency microsoft.net.test.sdk to v18

- **deps:** Update dependency microsoft.net.stringtools to 17.14.23

- **deps:** Update dependency microsoft.openapi to 2.3.4

- **deps:** Update github/codeql-action action to v4

- **deps:** Update dependency mongodb.entityframeworkcore to 9.0.2

- **deps:** Update dependency scalar.aspnetcore to 2.8.11

- **deps:** Update dependency microsoft.openapi to 2.3.5

- **deps:** Update dotnet monorepo

- **deps:** Update dependency scalar.aspnetcore to 2.9.0

- **deps:** Update dependency efcore.namingconventions to 10.0.0-rc.2

- **deps:** Update dependency npgsql.entityframeworkcore.postgresql to 10.0.0-rc.2

- **deps:** Update github/codeql-action digest to 16140ae

- **deps:** Update testcontainers-dotnet monorepo to 4.8.0

- **deps:** Update dependency microsoft.openapi to 2.3.6

- **deps:** Update testcontainers-dotnet monorepo to 4.8.1

- **deps:** Update dependency markdig to 0.43.0

- **deps:** Update aspire monorepo to 9.5.2

- **deps:** Update opentelemetry-dotnet-contrib monorepo to 1.13.0

- **deps:** Update github/codeql-action digest to 4e94bd1

- **deps:** Update mattraks/delete-workflow-runs digest to e284f4e

- **deps:** Update dependency mongodb.entityframeworkcore to 9.0.3

- **deps:** Update mattraks/delete-workflow-runs digest to 0073229

- **deps:** Update dependency microsoft.openapi to 2.3.8

- **deps:** Update Aspire.Hosting.Docker, KeyCloak, and KeyCloak.Authentication to version 9.5.2-preview.1.25522.3

- **deps:** Add Microsoft.Build.Tasks.Core package reference

- **deps:** Update github/codeql-action digest to 0499de3

- **deps:** Update mattraks/delete-workflow-runs digest to 86d29a7

- **deps:** Update dependency xunit.v3 to 3.2.0

- **deps:** Update dependency scalar.aspnetcore to 2.10.1

- **deps:** Update dependency nswag.apidescription.client to 14.6.2

- **deps:** Update dependency microsoft.openapi to 2.3.9

- **deps:** Update mattraks/delete-workflow-runs digest to 63b223f

- **deps:** Update dependency microsoft.entityframeworkcore.dynamiclinq to 9.6.10

- **deps:** Update dependency scalar.aspnetcore to 2.10.3

- **deps:** Update mattraks/delete-workflow-runs digest to 5bf9a1d

- **deps:** Update dependency microsoft.net.test.sdk to 18.0.1

- **deps:** Update dotnet monorepo

- **deps:** Update github/codeql-action digest to 014f16e

- **deps:** Update dependency sidio.sitemap.blazor to 1.2.0

- **deps:** Update dependency microsoft.entityframeworkcore.dynamiclinq to 9.7.0

- **deps:** Update opentelemetry-dotnet monorepo to 1.14.0

- **deps:** Update opentelemetry-dotnet-contrib monorepo to 1.14.0

- **deps:** Update actions/checkout digest to 93cb6ef

- **deps:** Update dependency microsoft.openapi to 2.3.10

- **deps:** Update dependency microsoft.codeanalysis.csharp to 5.0.0

- **deps:** Update github/codeql-action digest to e12f017

- **deps:** Update package versions and SDK references across the project

- **deps:** Replace Mongo2Go with Testcontainers.MongoDb for MongoDB integration tests

- **deps:** Update actions/checkout action to v6

- **deps:** Update dotnet monorepo

- **deps:** Update Microsoft.OpenApi version range to allow for future updates

- **deps:** Update package versions for Microsoft.AspNetCore and MongoDB dependencies

- **deps:** Update testcontainers-dotnet monorepo to 4.9.0

- **deps:** Update github/codeql-action digest to fdbfb4d

- **deps:** Update actions/setup-dotnet digest to 2016bd2

- **deps:** Update dependency markdig to 0.44.0

- **deps:** Update aspire monorepo to 13.0.1

- **deps:** Update dependency sonaranalyzer.csharp to 10.16.0.128591

- **deps:** Update dependency microsoft.entityframeworkcore.dynamiclinq to 10.7.1

- **deps:** Update dependency aspire.npgsql.entityframeworkcore.postgresql to v13

- **deps:** Update dependency xunit.v3 to 3.2.1

- **deps:** Update mongo-csharp-driver monorepo to 3.5.2

- **deps:** Update github/codeql-action digest to fe4161a

- **deps:** Update actions/checkout digest to 8e8c483

- **deps:** Update dependency sonaranalyzer.csharp to 10.16.1.129956

- **deps:** Update aspire monorepo to 13.0.2

- **deps:** Update dependency scalar.aspnetcore to 2.11.1

- **deps:** Update github/codeql-action digest to cf1bb45

- **deps:** Update dependency microsoft.openapi to 2.3.11

- **deps:** Update dotnet monorepo

- **deps:** Update dependency scalar.aspnetcore to 2.11.6

- **deps:** Update github/codeql-action digest to 1b168cd

- **deps:** Update github/codeql-action digest to 5d4e8d1

- **deps:** Update aspire monorepo to 13.1.0

- **deps:** Update dependency sonaranalyzer.csharp to 10.17.0.131074

- **deps:** Update dependency scalar.aspnetcore to 2.11.8

- **deps:** Update dependency microsoft.openapi to 2.4.1

- **deps:** Update dependency npgsql to 10.0.1

- **deps:** Update dependency npgsql.dependencyinjection to 10.0.1

- **deps:** Update dependency npgsql.opentelemetry to 10.0.1

- **deps:** Update dependency scalar.aspnetcore to 2.11.10

- **deps:** Update dependency keycloak.authservices.authorization to 2.8.0

- **deps:** Update testcontainers-dotnet monorepo to 4.10.0

- **deps:** Update dependency microsoft.openapi to 2.4.2

- **deps:** Update dependency sidio.sitemap.blazor to 1.3.0

- **deps:** Update dependency scalar.aspnetcore to 2.12.4

- **deps:** Update dependency sonaranalyzer.csharp to 10.18.0.131500

- **deps:** Update dependency efcore.namingconventions to 10.0.0

- **deps:** Update github/codeql-action digest to cdefb33

- **deps:** Update actions/setup-dotnet digest to baa11fb

- **deps:** Update mattraks/delete-workflow-runs digest to bd2822c

- **deps:** Update dependency xunit.v3 to 3.2.2

- **deps:** Update dotnet monorepo

- **deps:** Update mongo-csharp-driver monorepo to 3.6.0

- **deps:** Update dependency microsoft.openapi to 2.4.3

- **deps:** Update dependency scalar.aspnetcore to 2.12.11

- **deps:** Update dependency microsoft.openapi to 2.5.0

- **deps:** Update dependency mongodb.entityframeworkcore to 9.0.4

- **deps:** Update opentelemetry-dotnet monorepo to 1.15.0

- **deps:** Update opentelemetry-dotnet-contrib monorepo to 1.15.0

- **deps:** Update dependency efcore.namingconventions to 10.0.1

- **deps:** Update github/codeql-action digest to 19b2f06

- **deps:** Update dependency scalar.aspnetcore to 2.12.18

- **deps:** Update dependency microsoft.openapi to 2.6.1

- **deps:** Update github/codeql-action digest to b20883b

- **deps:** Update dependency scalar.aspnetcore to 2.12.24

- **deps:** Update dependency sonaranalyzer.csharp to 10.19.0.132793

- **deps:** Update dependency scalar.aspnetcore to 2.12.30

- **deps:** Update github/codeql-action digest to 6bc82e0

- **deps:** Update actions/checkout digest to de0fac2

- **deps:** Update github/codeql-action digest to 45cbd0c

- **deps:** Update dependency mongodb.entityframeworkcore to 9.1.0

- **deps:** Update dependency scalar.aspnetcore to 2.12.36

- **deps:** Update dependency fluentassertions to 7.2.1

- **deps:** Update dependency markdig to 0.45.0

- **deps:** Update aspire monorepo to 13.1.1

- **deps:** Update dotnet monorepo

- **deps:** Update github/codeql-action digest to 9e907b5

- **deps:** Update dependency scalar.aspnetcore to 2.12.41

- **deps:** Update dependency scalar.aspnetcore to 2.12.46

- **deps:** Update github/codeql-action digest to 89a39a4

- **deps:** Update dependency scalar.aspnetcore to 2.12.47

- **deps:** Update dependency microsoft.net.test.sdk to 18.3.0

- **deps:** Update aspire monorepo to 13.1.2

- **deps:** Update dependency sonaranalyzer.csharp to 10.20.0.135146

- **sln:** Remove legacy solution file and add new slnx format

- **deps:** Streamline container initialization in integration tests and fix MongoDB fixture

- **deps:** Update github/codeql-action digest to c793b71

- **deps:** Update mongo-csharp-driver monorepo to 3.7.0

- **deps:** Update dependency scalar.aspnetcore to 2.13.1

- **deps:** Update actions/setup-dotnet digest to c2fa09f

- **deps:** Update github/codeql-action digest to 0d579ff

- **deps:** Update dependency microsoft.openapi to 2.7.0

- **deps:** Update dependency mongodb.entityframeworkcore to 9.1.1

- **deps:** Update dependency npgsql to 10.0.2

- **deps:** Update dependency npgsql.dependencyinjection to 10.0.2

- **deps:** Update dependency npgsql.entityframeworkcore.postgresql to 10.0.1

- **deps:** Update github/codeql-action digest to b1bff81

- **deps:** Update dependency fluentassertions to 7.2.2

- **deps:** Update dependency npgsql.opentelemetry to 10.0.2

- **deps:** Update dependency opentelemetry.instrumentation.aspnetcore to 1.15.1

- **deps:** Update dotnet monorepo

- **deps:** Update mongo-csharp-driver monorepo to 3.7.1

- **deps:** Update github/codeql-action digest to 3869755

- **deps:** Update aspire monorepo to 13.1.3

- **deps:** Update dependency keycloak.authservices.authorization to 2.8.1

- **deps:** Update dependency microsoft.openapi to 2.7.1

- **deps:** Update aspire monorepo to 13.2.0

- **deps:** Update dependency sonaranalyzer.csharp to 10.21.0.135717

- **deps:** Update testcontainers-dotnet monorepo to 4.11.0

- **deps:** Update dependency scalar.aspnetcore to 2.13.15

- **deps:** Update github/codeql-action digest to c10b806

- **deps:** Update opentelemetry-dotnet monorepo to 1.15.1

- **deps:** Update mattraks/delete-workflow-runs digest to b301838

- **deps:** Update dependency keycloak.authservices.authorization to 2.9.0

- **deps:** Update GitHubActionsTestLogger version to 3.0.2

- **deps:** Remove GitHubActionsTestLogger package from project files

- Update package versions and remove obsolete settings file

- Update package versions and add OpenTelemetry.Api reference

- Update test configurations and thresholds for integration tests

- **deps:** Update github/codeql-action digest to e46ed2c

- **deps:** Update dependency microsoft.openapi to 2.7.4

- **deps:** Update aspire monorepo to 13.2.4

- **deps:** Update dependency scalar.aspnetcore to 2.14.9

- **deps:** Update dependency microsoft.entityframeworkcore.dynamiclinq to 10.7.2

- **deps:** Update dependency nswag.apidescription.client to 14.7.1

- **deps:** Update dependency sonaranalyzer.csharp to 10.25.0.139117

- **deps:** Update dependency sidio.sitemap.blazor to 2.0.2

- **deps:** Update dependency coverlet.collector to v10

- **deps:** Update dependency keycloak.authservices.authorization to v3

- **deps:** Update issue-labeler actions to v2.0.0-release

- **deps:** Update KeyCloak and related packages to latest preview versions

- **deps:** Add Shirubasoft.Aspire.Cloudflared package and configure Cloudflare tunnels

- **deps:** Update mongo-csharp-driver monorepo to 3.8.0

- **deps:** Update dependency markdig to 1.1.3

- **deps:** Update dotnet monorepo

- **deps:** Update dependency scalar.aspnetcore to 2.14.10


### Other

- Initial Commit

Signed-off-by: Bryan Gonzalez <bgonza868@gmail.com>

- Add production configuration files to .aiignore and .gitignore

- Add documentation for known limitations

- Add .gitkeep to data directory

- Refactor KeycloakRealmAdminClientExtensions for additional functionality

- Improve OpenAPI extensions with additional security schemes

- Add OpenID Connect authentication and configuration

- Update OpenAPI JSON with new security schemes

- Add secrets management in WebClient and MigrationService

- Enhance AppHost with dynamic container configuration

- Update AppHost project file for language version and cleanup

- Add container data paths to appsettings.json

- Remove obsolete container label limitation and update deployment PR status in LIMITATIONS.md

- Add multi-architecture container support and OpenAPI generation refinements to IHFiction.FictionApi

- Enable multi-architecture container builds for WebClient, MigrationService, and API projects

- Refactor Docker Compose deployment and add registry publishing support in AppHost

- Add deployCommandEnabled feature flag to Aspire settings

- Refactor configuration and improve API documentation

- Removed Keycloak service configuration from appsettings.Development.json.
- Added AllowedOrigins setting in appsettings.json for CORS configuration.
- Updated openapi.json to allow null values for certain response schemas and added descriptions for various properties.
- Enhanced Program.cs in WebClient to support Data Protection and Forwarded Headers in production.
- Simplified AppHost.cs by removing unnecessary container configuration and added support for Docker Swarm.
- Created Extensions.cs for reusable methods related to Docker image publishing and registry configuration.
- Updated ProductionConfigExtensions.cs to streamline Docker Compose configuration for Swarm.
- Removed deprecated Containers configuration from appsettings.json.
- Cleaned up MigrationService to remove unnecessary Development settings.
- Fixed MarkdownEditor component to handle JSDisconnectedException more gracefully.

- Refactor configuration files and remove unused Docker deployment classes

- Refactor configuration and improve OpenTelemetry integration with Dashboard API key handling

- Add LoaderService for global loading spinner and integrate with UI components

- Add Font Awesome 6.4.0 webfont files

- Added fa-brands-400.ttf and fa-brands-400.woff2
- Added fa-regular-400.ttf and fa-regular-400.woff2
- Added fa-solid-900.ttf and fa-solid-900.woff2
- Added fa-v4compatibility.ttf and fa-v4compatibility.woff2

These files are necessary for incorporating Font Awesome icons into the project.

- Sanitize CSP report logging to avoid logging raw user input (fixes CodeQL alert)

Refs: https://github.com/SheepReaper/IHeartFiction/security/code-scanning/2

What:
- Limit CSP report body size, parse JSON and extract only safe fields
  (document-uri, blocked-uri, violated-directive).
- Compute SHA256 fingerprint of the full report for traceability.
- Log only the safe fields + fingerprint (no raw user payload).
- Use structured LoggerMessage logging.

Why:
- Prevents log injection and exposure of arbitrary user payloads (CodeQL finding).

Files:
- src/lib/IHFiction.SharedWeb/Extensions/CspExtensions.cs

Risk: low — retains traceability via fingerprint while removing raw payload from logs.

- Add comprehensive copilot-instructions.md onboarding document

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Potential fix for code scanning alert no. 4: Workflow does not contain permissions

Co-authored-by: Copilot Autofix powered by AI <62310815+github-advanced-security[bot]@users.noreply.github.com>

- Potential fix for code scanning alert no. 3: Workflow does not contain permissions

Co-authored-by: Copilot Autofix powered by AI <62310815+github-advanced-security[bot]@users.noreply.github.com>

- Initial plan

Filter Works collection to only include Stories, not Chapters/Books

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

Add integration tests to verify Stories are counted correctly, not Chapters/Books

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Addressing PR comments

Co-authored-by: SheepReaper <5705509+SheepReaper@users.noreply.github.com>

- Add unit tests for author and story functionalities

- Implement tests for GetAuthor endpoint, including success and failure cases.
- Create unit tests for GetAuthor service, validating response mapping and handling various scenarios.
- Add tests for data shaping in GetAuthor responses, ensuring links are correctly flattened.
- Update author profile tests to reflect changes in request structure.
- Introduce tests for adding chapters to stories, validating request data.
- Implement tests for retrieving own stories, including validation and response construction.
- Add tests for getting story details, ensuring correct response structure.
- Create tests for listing story chapters, validating chapter item creation.
- Update publish story tests to reflect changes in response structure and naming conventions.

- Refactor Routes.razor to remove unnecessary CascadingAuthenticationState wrapper

Add EndpointRegistrationGenerator for automatic endpoint and use case registration

Create IHFiction.SourceGenerators project with necessary dependencies

- Refactor Program.cs to improve build environment detection and adjust HTTPS redirection logic, Switch to slim builder

- Refactor query parameters to use non-nullable types with default values

- Updated various query records across the application to replace nullable string and integer properties with non-nullable types, providing default values where applicable.
- Adjusted the IDataShapingSupport, IPaginationSupport, ISearchSupport, and ISortingSupport interfaces to reflect these changes.
- Modified OpenAPI documentation to include default values for query parameters.
- Ensured consistency in handling pagination, sorting, and searching across different API endpoints.

- Enhance OpenApiExtensions and Program.cs for improved type handling and HTTPS support in development

- Refactor data loading in AuthorizationService and EntityLoaderService for improved clarity and performance; update KeycloakAdminService to utilize new serializer context; enhance OpenApiExtensions and SearchExtensions with necessary imports; adjust ListPublishedStoryChapters to use FictionDbContext directly; addressed all AOT Analyzer warnings..

- Refactor author and story endpoints to improve link generation and validation

- Updated GetAuthor and ListAuthors endpoints to use KeyValuePair for link generation.
- Enhanced KeycloakRealmAdminClientExtensions with dynamic member access attributes.
- Simplified pagination options and validation extensions by removing unused methods.
- Adjusted response mapping service to streamline result creation.
- Modified AddTagsToStory and UpdateOwnAuthorProfile to enforce string length constraints.
- Cleaned up integration and unit tests by removing redundant validations and tests for invalid cases.
- Removed unnecessary extension methods and tests related to chapter content updates.
- Updated OpenAPI documentation to reflect new validation rules for tags.
- Addressed all trim warnings

- Refactor story handling to use MongoDB for WorkBody storage

- Updated CreateBookChapter, CreateStory, and CreateStoryChapter to use IMongoCollection<WorkBody> instead of StoryDbContext for WorkBody operations.
- Modified DeleteChapter to delete WorkBody from MongoDB.
- Adjusted GetPublishedChapterContent and GetPublishedStoryContent to retrieve WorkBody from MongoDB.
- Refactored UpdateChapterContent and UpdateStoryContent to update WorkBody in MongoDB.
- Removed StoryDbContext and related design-time factory as it is no longer needed.
- Updated integration tests to use MongoDB for WorkBody operations.
- Added MongoDbFixture for unit tests to manage MongoDB lifecycle.
- Updated project references to include necessary MongoDB and Npgsql packages.

- Refactor MongoDB service registration to use IMongoDatabase for improved clarity

- Add Playwright CLI documentation and features

- Introduced tracing capabilities for detailed execution analysis, including action logs, network activity, and DOM snapshots.
- Added video recording functionality for browser automation sessions, supporting WebM format.
- Enhanced the CLI command reference for the Aspire skill with rebuild options for .NET projects.
- Created comprehensive documentation for Playwright CLI, covering commands, session management, storage state, request mocking, and test generation.
- Implemented best practices and limitations for tracing and video recording to guide users in effective usage.

- Enhance UI components and improve styling

- Updated .gitignore to exclude local Playwright test results and agent generated files.
- Refactored AuthorCard, AuthorDetail, AuthorList, and Story components to improve styling and accessibility.
- Replaced hardcoded colors with CSS classes for better theme support.
- Improved tooltip handling and removed unnecessary CSS for tooltips.
- Added Playwright UI audit script for automated testing of UI components.
- Enhanced button styles and hover effects for better user experience.
- Cleaned up unused CSS and improved overall layout consistency.

Co-authored-by: Copilot <copilot@github.com>

- Potential fix for code scanning alert no. 5: Disabling certificate validation

Co-authored-by: Copilot Autofix powered by AI <62310815+github-advanced-security[bot]@users.noreply.github.com>

- Enhance migration handling and API client generation

- Added migration service instructions for local Aspire runs, emphasizing migration-first signals and required order for debugging.
- Updated .gitignore to include agent generated artifacts.
- Documented migration application and schema mismatch guardrails in AGENTS.md.
- Introduced NSwag.CodeGeneration.CSharp package for API client generation.
- Added IHFiction.ApiClientGenerator project to the solution for generating API clients.
- Improved OpenApiExtensions to handle Ulid and ObjectId types more effectively in OpenAPI schemas.
- Updated FictionApiJsonContext to include serialization for ObjectId.
- Modified openapi.json to refine schema definitions for Ulid and ObjectId.
- Refactored HttpClient registration in WebClient to use the new FictionApiClient directly.
- Integrated Snappier package for improved performance in various projects.
- Created CustomFormatTypeResolver to handle custom formats for Ulid and ObjectId in API client generation.
- Updated services to replace IFictionApiClient with the new FictionApiClient, ensuring type safety with Ulid.
- Enhanced StoryService and WorkService to utilize Ulid directly instead of string representations.


### Tests

- Adjust performance threshold for large dataset handling in PostgreSQL tests


