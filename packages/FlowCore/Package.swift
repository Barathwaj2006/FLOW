// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "FlowCore",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "FlowCore",
            targets: ["FlowCore"]
        ),
    ],
    dependencies: [
        // Managed dependencies pinned in DEPENDENCIES.md:
        // .package(url: "https://github.com/groue/GRDB.swift.git", exact: "6.29.0"),
        // .package(url: "https://github.com/argmaxinc/WhisperKit.git", exact: "0.9.0"),
    ],
    targets: [
        .target(
            name: "FlowCore",
            dependencies: [],
            path: "Sources"
        ),
        .testTarget(
            name: "FlowCoreTests",
            dependencies: ["FlowCore"],
            path: "Tests"
        ),
    ]
)
