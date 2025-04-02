import http from "k6/http";

export const options = {
    vus: 10,
    duration: '30s',
  };
  

export default function() {
    let res = http.get("https://localhost:7270/token");
    console.log(res.status);
}