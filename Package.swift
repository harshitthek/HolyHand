// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "HolyHand",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "HolyHand",
            path: "Sources/HolyHand"
        ),
        .testTarget(name: "HolyHandTests", dependencies: ["HolyHand"])
    ]
)
