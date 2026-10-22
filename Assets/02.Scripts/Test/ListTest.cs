using UnityEngine;
using System.Collections.Generic;

public class ListTest : MonoBehaviour
{
    //자료구조(Data Structure)
    // 이런 데이터들을 '효율적'으로 관리(CRUDS)하기 위해 저장하는 방식
    // C(Create), R(Read), U(Update), D(Delete), S(Search)
    // B(Browse), R(Read), E(Edit), A(Add), D(Delete)
    // 선형 vs 비선형 자료구조
    // 선형 자료구조 : 배열, 리스트, 스택, 큐
    // 비선형 자료구조 : 트리, 그래프, 해쉬테이블

    private void Start()
    {
        int[] ages = new int[] { 22, 21, 34, 22 }; //배열의 선언과 정의
        // 1. 배열은 선언할 때 지정한 배열의 크기 변경 X
        // 그래서 배열에 데이터를 추가나 삭제하기 어려움 (비효율적인 과정이 필요하다.)
        ages[0] = 10;
        ages[1] = 20;
        ages[2] = 30;
        ages[3] = 40;
        // ages[4] = 50; << 범위를 벗어나면 IndexOutOfRange 오류가 뜸

        // 동적 배열 vs 연결 리스트
        // C#을 이것을 "콜렉션" 네임스페이스 안에 리스트로 구현해놓음
        // Collection : 컴퓨터공학의 자료구조를 C#에서 구조화하여 클래스로 구현해 놓은 것들 

        // List : 배열과 똑같은데 필요에 따라 크기가 동적으로 증가하고 감소하는 배열
        // using System.Collections.Generic;
        List<int> ages2 = new List<int>() { 5, 10, 14 };
    }
}
