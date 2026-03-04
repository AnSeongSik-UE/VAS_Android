# -------------------------------------------------------------
# 1단계 (빌더): 속도와 가벼움을 위해 Go를 빌드용 운영체제로 잠시 사용합니다
# -------------------------------------------------------------
FROM golang:1.22-alpine AS builder
WORKDIR /app
COPY main.go .
RUN go mod init relay_server && go mod tidy
RUN CGO_ENABLED=0 GOOS=linux go build -o relay_server .

# -------------------------------------------------------------
# 2단계 (런타임): 아주 가볍고 뼈대만 있는 알파인 리눅스 이미지에 우리가 방금 만든 빌드 파일 하나만 쏙 넣습니다!
# 이렇게 하면 Docker 이미지 크기가 보통 1GB 넘어가는 데 단 10MB 내외로 압도적으로 줄어듭니다.
# -------------------------------------------------------------
FROM alpine:latest  
WORKDIR /root/
COPY --from=builder /app/relay_server .
EXPOSE 49152/udp
CMD ["./relay_server"]
