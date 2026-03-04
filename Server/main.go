// main.go
// 안드로이드 앱에서 보낸 데이터를 PC 앱으로 뿌려주는 초저지연 UDP 중계(Relay) 서버
package main

import (
	"fmt"
	"log"
	"net"
	"sync"
)

func main() {
	// 1. 서버가 수신할 UDP 포트 설정 (알기 쉽게 번호 지정)
	port := 49152

	// C#의 IPEndPoint와 유사. 포트를 열 준비를 합니다.
	addr := net.UDPAddr{
		Port: port,
		IP:   net.ParseIP("0.0.0.0"), // 모든 외부 IP 구별없이 다 받음
	}

	// 2. Create and Bind socket
	conn, err := net.ListenUDP("udp", &addr)
	if err != nil {
		log.Fatalf("Server creation error: %v\n", err)
	}
	defer conn.Close()

	fmt.Printf("🚀 Golang UDP Relay Server listening on port %d...\n", port)

	// 3. Client list (PC apps)
	clients := make(map[string]*net.UDPAddr)
	var mutex sync.Mutex

	// 4. Allocate buffer (260 bytes per packet, 1024 for safety)
	buffer := make([]byte, 1024)

	// 5. Infinite loop for packet reception
	for {
		n, remoteAddr, err := conn.ReadFromUDP(buffer)
		if err != nil {
			continue
		}

		addrString := remoteAddr.String()

		// 6. Registration via "SUBSCRIBE" keyword
		if n == 9 && string(buffer[:n]) == "SUBSCRIBE" {
			mutex.Lock()
			clients[addrString] = remoteAddr
			mutex.Unlock()
			fmt.Printf("✅ New watcher subscribed (PC added): %s\n", addrString)
			continue
		}

		// 7. Relay tracking packet (260 bytes) to all subscribers
		mutex.Lock()
		for _, clientAddr := range clients {
			conn.WriteToUDP(buffer[:n], clientAddr)
		}
		mutex.Unlock()
	}
}
