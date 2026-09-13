// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "FlowMacOS",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "FlowMacOS",
            targets: ["FlowMacOS"]
        ),
    ],
    dependencies: [
        .package(path: "../FlowCore"),
    ],
    targets: [
        .target(
            name: "FlowMacOS",
            dependencies: [
                .product(name: "FlowCore", package: "FlowCore"),
            ],
            path: "Sources"
        ),
        .testTarget(
            name: "FlowMacOSTests",
            dependencies: ["FlowMacOS"],
            path: "Tests"
        ),
    ]
)
